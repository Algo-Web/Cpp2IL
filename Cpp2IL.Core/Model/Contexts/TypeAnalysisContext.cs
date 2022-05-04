using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LibCpp2IL.BinaryStructures;
using LibCpp2IL.Metadata;
using LibCpp2IL.Reflection;
using StableNameDotNet.Providers;

namespace Cpp2IL.Core.Model.Contexts;

/// <summary>
/// Represents one managed type in the application.
/// </summary>
public class TypeAnalysisContext : HasCustomAttributesAndName, ITypeInfoProvider
{
    /// <summary>
    /// The context for the assembly this type was defined in.
    /// </summary>
    public readonly AssemblyAnalysisContext DeclaringAssembly;

    /// <summary>
    /// The underlying metadata for this type. Allows access to RGCTX data, the raw bitfield properties, interfaces, etc.
    /// </summary>
    public readonly Il2CppTypeDefinition? Definition;

    /// <summary>
    /// The analysis contexts for methods contained within this type.
    /// </summary>
    public readonly List<MethodAnalysisContext> Methods;

    /// <summary>
    /// The analysis contexts for properties contained within this type.
    /// </summary>
    public readonly List<PropertyAnalysisContext> Properties;

    /// <summary>
    /// The analysis contexts for events contained within this type.
    /// </summary>
    public readonly List<EventAnalysisContext> Events;

    /// <summary>
    /// The analysis contexts for fields contained within this type.
    /// </summary>
    public readonly List<FieldAnalysisContext> Fields;

    /// <summary>
    /// The analysis contexts for nested types within this type.
    /// </summary>
    public List<TypeAnalysisContext> NestedTypes { get; internal set; } = new();

    protected override int CustomAttributeIndex => Definition!.CustomAttributeIndex;

    public override AssemblyAnalysisContext CustomAttributeAssembly => DeclaringAssembly;

    public override string DefaultName => Definition?.Name! ?? throw new("Subclasses of TypeAnalysisContext must override DefaultName");

    public virtual string DefaultNs => Definition?.Namespace ?? throw new("Subclasses of TypeAnalysisContext must override DefaultNs");

    public string? OverrideNs { get; set; }

    public string Namespace => OverrideNs ?? DefaultNs;

    public TypeAnalysisContext? OverrideBaseType { get; protected set; }
    
    public TypeAnalysisContext? DeclaringType { get; protected internal set; }

    public TypeAnalysisContext(Il2CppTypeDefinition? il2CppTypeDefinition, AssemblyAnalysisContext containingAssembly) : base(il2CppTypeDefinition?.Token ?? 0, containingAssembly.AppContext)
    {
        DeclaringAssembly = containingAssembly;
        Definition = il2CppTypeDefinition;

        if (Definition != null)
        {
            InitCustomAttributeData();

            Methods = Definition.Methods!.Select(m => new MethodAnalysisContext(m, this)).ToList();
            Properties = Definition.Properties!.Select(p => new PropertyAnalysisContext(p, this)).ToList();
            Events = Definition.Events!.Select(e => new EventAnalysisContext(e, this)).ToList();
            Fields = Definition.FieldInfos!.ToList().Select(f => new FieldAnalysisContext(f, this)).ToList();
        }
        else
        {
            Methods = new();
            Properties = new();
            Events = new();
            Fields = new();
        }
    }

    public MethodAnalysisContext? GetMethod(Il2CppMethodDefinition? methodDefinition)
    {
        if (methodDefinition == null)
            return null;

        return Methods.Find(m => m.Definition == methodDefinition);
    }

    public List<MethodAnalysisContext> GetConstructors() => Methods.Where(m => m.Definition!.Name == ".ctor").ToList();

    public override string ToString() => $"Type: {Definition?.FullName}";

    #region StableNameDotNet implementation

    public IEnumerable<ITypeInfoProvider> GetBaseTypeHierarchy()
    {
        if (OverrideBaseType != null)
            throw new("Type hierarchy for injected types is not supported");

        var baseType = Definition!.RawBaseType;
        while (baseType != null)
        {
            yield return GetSndnProviderForType(AppContext, baseType);

            baseType = baseType.CoerceToUnderlyingTypeDefinition().RawBaseType;
        }
    }

    public static ITypeInfoProvider GetSndnProviderForType(ApplicationAnalysisContext appContext, Il2CppType type)
    {
        if (type.Type == Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST)
        {
            var genericClass = type.GetGenericClass();
            var elementType = appContext.ResolveContextForType(genericClass.TypeDefinition)!;

            var genericParamTypes = genericClass.Context.ClassInst.Types;

            if (genericParamTypes.Any(t => t.Type is Il2CppTypeEnum.IL2CPP_TYPE_VAR or Il2CppTypeEnum.IL2CPP_TYPE_MVAR))
                //Discard non-fixed generic instances
                return elementType;

            var genericArguments = genericParamTypes.Select(t => GetSndnProviderForType(appContext, t)).ToArray();

            return new GenericInstanceTypeInfoProviderWrapper(elementType, genericArguments);
        }

        if (type.Type is Il2CppTypeEnum.IL2CPP_TYPE_VAR or Il2CppTypeEnum.IL2CPP_TYPE_MVAR)
            return new GenericParameterTypeInfoProviderWrapper(type.GetGenericParameterDef().Name!);

        if (type.Type is Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY or Il2CppTypeEnum.IL2CPP_TYPE_PTR)
            return GetSndnProviderForType(appContext, type.GetEncapsulatedType());

        if (type.Type is Il2CppTypeEnum.IL2CPP_TYPE_ARRAY)
            return GetSndnProviderForType(appContext, type.GetArrayElementType());

        if (type.Type.IsIl2CppPrimitive())
            return appContext.ResolveContextForType(LibCpp2IlReflection.PrimitiveTypeDefinitions[type.Type])!;

        return appContext.ResolveContextForType(type.AsClass())!;
    }

    public IEnumerable<ITypeInfoProvider> Interfaces => Definition!.RawInterfaces!.Select(t => GetSndnProviderForType(AppContext, t));
    public TypeAttributes TypeAttributes => Definition!.Attributes;
    public string TypeName => DefaultName;
    public bool IsGenericInstance => false;
    public bool IsValueType => Definition!.IsValueType;
    public bool IsEnumType => Definition!.IsEnumType;
    public IEnumerable<ITypeInfoProvider> GenericArgumentInfoProviders => Array.Empty<ITypeInfoProvider>();
    public IEnumerable<IFieldInfoProvider> FieldInfoProviders => Fields;
    public IEnumerable<IMethodInfoProvider> MethodInfoProviders => Methods;
    public IEnumerable<IPropertyInfoProvider> PropertyInfoProviders => Properties;

    #endregion
}