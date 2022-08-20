﻿namespace Arm64Disassembler.InternalDisassembly;

public static class Arm64DataProcessingImmediate
{
    public static Arm64Instruction Disassemble(uint instruction)
    {
        //This one, at least, is mercifully simple
        var op0 = (instruction >> 23) & 0b111; //Bits 23-25
        
        //All 8 possible variants are defined, as 7 subcategories.
        return op0 switch
        {
            0b000 or 0b001 => PcRelativeAddressing(instruction),
            0b010 => AddSubtractImmediate(instruction),
            0b011 => AddSubtractImmediateWithTags(instruction),
            0b100 => LogicalImmediate(instruction),
            0b101 => MoveWideImmediate(instruction),
            0b110 => Bitfield(instruction),
            0b111 => Extract(instruction),
            _ => throw new ArgumentOutOfRangeException(nameof(op0), "Impossible op0 value")
        };
    }

    public static Arm64Instruction PcRelativeAddressing(uint instruction)
    {
        throw new NotImplementedException();
    }

    public static Arm64Instruction AddSubtractImmediate(uint instruction)
    {
        var is64Bit = instruction.TestBit(31); //sf flag
        var isSubtract = instruction.TestBit(30); //op flag
        var setFlags = instruction.TestBit(29); //S flag
        var shiftLeftBy12 = instruction.TestBit(22);

        var imm12 = (ulong) (instruction >> 10) & 0b1111_1111_1111;
        var rn = (int) (instruction >> 5) & 0b1_1111;
        var rd = (int) instruction & 0b1_1111;
        
        if(shiftLeftBy12)
            imm12 <<= 12;

        var mnemonic = isSubtract switch
        {
            true when setFlags => Arm64Mnemonic.SUBS,
            true => Arm64Mnemonic.SUB,
            false when setFlags => Arm64Mnemonic.ADDS,
            false => Arm64Mnemonic.ADD
        };

        var regN = Arm64Register.X0 + rn;
        var regD = Arm64Register.X0 + rd;
        var immediate = is64Bit switch
        {
            true => imm12,
            false => (uint) imm12
        };

        return new()
        {
            Mnemonic = mnemonic,
            Op0Kind = Arm64OperandKind.Register,
            Op1Kind = Arm64OperandKind.Register,
            Op2Kind = Arm64OperandKind.Immediate,
            Op0Reg = regD,
            Op1Reg = regN,
            Op2Imm = immediate
        };
    }

    public static Arm64Instruction AddSubtractImmediateWithTags(uint instruction)
    {
        throw new NotImplementedException();
    }

    public static Arm64Instruction LogicalImmediate(uint instruction)
    {
        throw new NotImplementedException();
    }

    public static Arm64Instruction MoveWideImmediate(uint instruction)
    {
        var is64Bit = instruction.TestBit(31);
        var opc = (instruction >> 29) & 0b11;
        var hw = (instruction >> 21) & 0b11;
        var imm16 = (instruction >> 5) & 0b1111_1111_1111_1111;
        var rd = (int) instruction & 0b1_1111;
        
        if(opc == 0b01)
            throw new Arm64UndefinedInstructionException("Move wide immediate with opc == 0b01");
        
        if(!is64Bit && hw.TestBit(1))
            throw new Arm64UndefinedInstructionException("Move wide immediate with hw bit 1 and !is64Bit");

        var mnemonic = opc switch
        {
            0b00 => Arm64Mnemonic.MOVN, //Move not
            0b10 => Arm64Mnemonic.MOVZ, //Move zero
            0b11 => Arm64Mnemonic.MOVK,
            _ => throw new("Impossible opc value")
        };

        var baseReg = is64Bit ? Arm64Register.X0 : Arm64Register.W0;
        
        var regD = baseReg + rd;
        var shift = (int) hw * 16;

        imm16 <<= shift;
        
        return new()
        {
            Mnemonic = mnemonic,
            Op0Kind = Arm64OperandKind.Register,
            Op1Kind = Arm64OperandKind.Immediate,
            Op0Reg = regD,
            Op1Imm = imm16
        };
    }

    public static Arm64Instruction Bitfield(uint instruction)
    {
        throw new NotImplementedException();
    }

    public static Arm64Instruction Extract(uint instruction)
    {
        throw new NotImplementedException();
    }
}