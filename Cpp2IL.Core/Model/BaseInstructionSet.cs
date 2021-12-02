using Cpp2IL.Core.Graphs;
using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL.Metadata;

namespace Cpp2IL.Core.Model;

public abstract class BaseInstructionSet
{
    /// <summary>
    /// Build a Control Flow Graph from the given method. The raw method body can be accessed via the <see cref="MethodAnalysisContext.RawBytes"/> property.
    /// </summary>
    /// <param name="context">The analysis context for the method to build the control flow graph for.</param>
    /// <returns>A type implementing <see cref="IControlFlowGraph"/> that can be used to build a control flow graph for this method.</returns>
    public abstract IControlFlowGraph BuildGraphForMethod(MethodAnalysisContext context);

    /// <summary>
    /// Get the raw method body for the given method.
    /// </summary>
    /// <param name="context">The method to get the body for</param>
    /// <param name="isAttributeGenerator">True if this is an attribute generator function, false if it's a managed method</param>
    /// <returns>A byte array representing the method's body</returns>
    public abstract byte[] GetRawBytesForMethod(MethodAnalysisContext context, bool isAttributeGenerator);

    /// <summary>
    /// Returns the virtual address from which the given method starts. By default, returns the <see cref="Il2CppMethodDefinition.MethodPointer"/> property, but
    /// can be overridden to provide a different value for instruction sets where this is necessary, for example WASM.
    /// </summary>
    /// <param name="context">The analysis context for the method to return the pointer for.</param>
    /// <returns></returns>
    public virtual ulong GetPointerForMethod(MethodAnalysisContext context) => context.Definition.MethodPointer;
}