﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cpp2IL.Core.Exceptions;

namespace Cpp2IL.Core.Graphs;

public class InstructionGraphNode<T> : IControlFlowNode
{
    public int ID { get; set; }

    public bool IsConditionalBranch => _flowControl == InstructionGraphNodeFlowControl.ConditionalJump;

    public Condition<T>? Condition { get; protected set; }
    public InstructionGraphNode<T>? TrueTarget { get; protected set; }
    public InstructionGraphNode<T>? FalseTarget { get; protected set; }

    public InstructionGraphNode()
    {
        Instructions = new();
        Successors = new();
        Predecessors = new();
    }

    public InstructionGraphNodeSet<T> Successors { get; set; }
    public InstructionGraphNodeSet<T> Predecessors { get; set; }

    private InstructionGraphNodeFlowControl? _flowControl;

    public bool HasProcessedSuccessors = false;

    public bool NeedsCorrectingDueToJump = false;

    public bool Visited = false;

    public BitArray? Dominators;
        
    public InstructionGraphNodeFlowControl? FlowControl
    {
        get => _flowControl;
        set => _flowControl = value;
    }
        
    public void AddInstruction(T instruction) => Instructions.Add(instruction);

    public void CheckCondition()
    {
        if (Successors.Count != 2)
        {
            throw new Exception($"Node didn't have 2 neighbours, instead had {Successors.Count}, aborting...\n\nNode Dump:\n{GetTextDump()}");
        }

        var node = this;
        while(!node.ThisNodeHasComparison())
            node = node.Predecessors.Count == 1 ? node.Predecessors.Single() : throw new NodeConditionCalculationException("Don't have a comparison and don't have a single predecessor line to a node which has one");

        CreateCondition(node.GetLastComparison());
    }

    protected virtual void CreateCondition(T comparison)
    {
        throw new NotImplementedException();
    }

    protected virtual T GetLastComparison() => throw new NotImplementedException();

    protected string GetTextDump()
    {
        StringBuilder stringBuilder = new();
        stringBuilder.Append($"ID: {ID}, FlowControl: {_flowControl}");
        if (IsConditionalBranch)
            stringBuilder.Append($", Condition: {Condition?.ConditionString}");
        stringBuilder.Append('\n');
        foreach (var instruction in Instructions)
        {
            stringBuilder.AppendLine(GetFormattedInstructionAddress(instruction) + " " + instruction.ToString());
        }
        return stringBuilder.ToString();
    }

    public virtual string GetFormattedInstructionAddress(T instruction) => throw new NotImplementedException();
    public virtual bool ThisNodeHasComparison()
    {
        throw new NotImplementedException();
    }

    public List<T> Instructions { get; } = new();
}