using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cpp2IL.Core.Extensions;
using Cpp2IL.Core.Logging;
using Iced.Intel;
using LibCpp2IL;
using LibCpp2IL.Metadata;

namespace Cpp2IL.Core.Utils
{
    public static class X86Utils
    {
        private static readonly Regex UpscaleRegex = new Regex("(?:^|([^a-zA-Z]))e([a-z]{2})", RegexOptions.Compiled);
        private static readonly ConcurrentDictionary<string, string> CachedUpscaledRegisters = new();
        private static readonly ConcurrentDictionary<Register, string> CachedX86RegNamesNew = new();
        
        public static InstructionList Disassemble(byte[] bytes, ulong methodBase)
        {
            var codeReader = new ByteArrayCodeReader(bytes);
            var decoder = Decoder.Create(LibCpp2IlMain.Binary!.is32Bit ? 32 : 64, codeReader);
            decoder.IP = methodBase;
            var instructions = new InstructionList();
            var endRip = decoder.IP + (uint)bytes.Length;

            while (decoder.IP < endRip)
                instructions.Add(decoder.Decode());

            return instructions;
        }

        public static byte[] GetRawManagedOrCaCacheGenMethodBody(ulong ptr, bool isCaGen)
        {
            var rawAddr = (int) LibCpp2IlMain.Binary!.MapVirtualAddressToRaw(ptr);
            var virtStartNextFunc = MiscUtils.GetAddressOfNextFunctionStart(ptr);

            if (virtStartNextFunc == 0 || (isCaGen && virtStartNextFunc - ptr > 50000))
            {
                GetMethodBodyAtVirtAddressNew(ptr, false, out var ret);
                return ret;
            }

            var startOfNextFunc = (int) LibCpp2IlMain.Binary.MapVirtualAddressToRaw(virtStartNextFunc);

            var retList = LibCpp2IlMain.Binary.GetRawBinaryContent().SubArray(rawAddr..startOfNextFunc).ToList();
            
            retList.TrimEndWhile(i => i == 0xCC);

            return retList.ToArray();
        }

        public static InstructionList GetManagedMethodBody(Il2CppMethodDefinition method) => Disassemble(GetRawManagedOrCaCacheGenMethodBody(method.MethodPointer, false), method.MethodPointer);

        public static InstructionList GetMethodBodyAtVirtAddressNew(ulong addr, bool peek) => GetMethodBodyAtVirtAddressNew(addr, peek, out _);

        public static InstructionList GetMethodBodyAtVirtAddressNew(ulong addr, bool peek, out byte[] rawBytes)
        {
            var functionStart = addr;
            var ret = new InstructionList();
            var con = true;
            var buff = new List<byte>();
            var rawAddr = LibCpp2IlMain.Binary!.MapVirtualAddressToRaw(addr);
            var startOfNextFunc = MiscUtils.GetAddressOfNextFunctionStart(addr);

            if (rawAddr < 0 || rawAddr >= LibCpp2IlMain.Binary.RawLength)
            {
                Logger.ErrorNewline($"Invalid call to GetMethodBodyAtVirtAddressNew, virt addr {addr} resolves to raw {rawAddr} which is out of bounds");
                rawBytes = Array.Empty<byte>();
                return ret;
            }

            while (con)
            {
                if (addr >= startOfNextFunc)
                    break;
                
                buff.Add(LibCpp2IlMain.Binary.GetByteAtRawAddress((ulong)rawAddr));

                ret = X86Utils.Disassemble(buff.ToArray(), functionStart);

                if (ret.All(i => i.Mnemonic != Mnemonic.INVALID) && ret.Any(i => i.Code == Code.Int3))
                    con = false;

                if (peek && buff.Count > 50)
                    con = false;
                else if (buff.Count > 50000)
                    con = false; //Sanity breakout.

                addr++;
                rawAddr++;
            }

            rawBytes = buff.ToArray();

            return ret;
        }

        public static string UpscaleRegisters(string replaceIn)
        {
            if (CachedUpscaledRegisters.ContainsKey(replaceIn))
                return CachedUpscaledRegisters[replaceIn];

            if (replaceIn.Length < 2) return replaceIn;

            //Special case the few 8-bit register: "al" => "rax" etc
            if (replaceIn == "al")
                return "rax";
            if (replaceIn == "bl")
                return "rbx";
            if (replaceIn == "dl")
                return "rdx";
            if (replaceIn == "ax")
                return "rax";
            if (replaceIn == "cx" || replaceIn == "cl")
                return "rcx";

            //R9d, etc.
            if (replaceIn[0] == 'r' && replaceIn[^1] == 'd')
                return replaceIn.Substring(0, replaceIn.Length - 1);

            var ret = UpscaleRegex.Replace(replaceIn, "$1r$2");
            CachedUpscaledRegisters.TryAdd(replaceIn, ret);

            return ret;
        }

        public static string GetFloatingRegister(string original)
        {
            switch (original)
            {
                case "rcx":
                    return "xmm0";
                case "rdx":
                    return "xmm1";
                case "r8":
                    return "xmm2";
                case "r9":
                    return "xmm3";
                default:
                    return original;
            }
        }

        public static string GetRegisterName(Register register)
        {
            if (register == Register.None) return "";

            if (!CachedX86RegNamesNew.TryGetValue(register, out var ret))
            {
                if (!register.IsVectorRegister())
                    ret = register.GetFullRegister().ToString().ToLowerInvariant();
                else
                    ret = UpscaleRegisters(register.ToString().ToLower());
                
                CachedX86RegNamesNew[register] = ret;
            }

            return ret;
        }
    }
}