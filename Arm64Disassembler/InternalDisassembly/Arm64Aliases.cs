﻿namespace Arm64Disassembler.InternalDisassembly;

public class Arm64Aliases
{
    public static void CheckForAlias(ref Arm64Instruction instruction)
    {
        if (instruction.Mnemonic == Arm64Mnemonic.ORR && instruction.Op2Imm == 0 && instruction.Op1Reg is Arm64Register.X31 or Arm64Register.W31)
        {
            //Change ORR R1, X31, R2, 0 to MOV R1, R2
            instruction.Mnemonic = Arm64Mnemonic.MOV;
            
            //Clear immediate
            instruction.Op3Imm = 0;
            instruction.Op3Kind = Arm64OperandKind.None;
            
            //Copy op2 to op1
            instruction.Op1Reg = instruction.Op2Reg;
            instruction.Op2Reg = Arm64Register.INVALID;
            
            //Clear op2
            instruction.Op2Kind = Arm64OperandKind.None;
            
            return;
        }

        if (instruction.Mnemonic == Arm64Mnemonic.SUBS && instruction.Op0Kind == Arm64OperandKind.Register && instruction.Op0Reg == Arm64Register.W31 && instruction.Op1Kind == Arm64OperandKind.Register && instruction.Op2Kind == Arm64OperandKind.Immediate)
        {
            //SUBS W31, WXX, IMM => CMP WXX, IMM
            
            //Convert mnemonic
            instruction.Mnemonic = Arm64Mnemonic.CMP;
            
            //Shift operands down
            instruction.Op0Reg = instruction.Op1Reg;
            instruction.Op1Kind = Arm64OperandKind.Immediate;
            instruction.Op2Kind = Arm64OperandKind.None;
            instruction.Op1Imm = instruction.Op2Imm;
            
            //Null op2
            instruction.Op2Imm = 0;
            instruction.Op1Reg = Arm64Register.INVALID;
            
            return;
        }
    }
}