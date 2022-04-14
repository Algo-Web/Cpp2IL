using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using LibCpp2IL.BinaryStructures;
using LibCpp2IL.Logging;
using LibCpp2IL.Metadata;
using LibCpp2IL.Reflection;

namespace LibCpp2IL
{
    public static class LibCpp2ILUtils
    {
        private static readonly Dictionary<int, string> TypeString = new Dictionary<int, string>
        {
            {1, "void"},
            {2, "bool"},
            {3, "char"},
            {4, "sbyte"},
            {5, "byte"},
            {6, "short"},
            {7, "ushort"},
            {8, "int"},
            {9, "uint"},
            {10, "long"},
            {11, "ulong"},
            {12, "float"},
            {13, "double"},
            {14, "string"},
            {22, "TypedReference"},
            {24, "IntPtr"},
            {25, "UIntPtr"},
            {28, "object"}
        };

        private static readonly Dictionary<string, ulong> PrimitiveSizes = new()
        {
            {"Byte", 1},
            {"SByte", 1},
            {"Boolean", 1},
            {"Int16", 2},
            {"UInt16", 2},
            {"Char", 2},
            {"Int32", 4},
            {"UInt32", 4},
            {"Single", 4},
            {"Int64", 8},
            {"UInt64", 8},
            {"Double", 8},
            {"IntPtr", 8},
            {"UIntPtr", 8},
        };

        private static Dictionary<FieldInfo, VersionAttribute[]> _cachedVersionAttributes = new();

        internal static void Reset()
        {
            _cachedVersionAttributes.Clear();
        }

        internal static string GetTypeName(Il2CppMetadata metadata, Il2CppBinary cppAssembly, Il2CppTypeDefinition typeDef, bool fullName = false)
        {
            var ret = string.Empty;
            if (fullName)
            {
                ret = typeDef.Namespace;
                if (ret != string.Empty)
                {
                    ret += ".";
                }
            }

            if (typeDef.DeclaringTypeIndex != -1)
            {
                ret += GetTypeName(metadata, cppAssembly, cppAssembly.GetType(typeDef.DeclaringTypeIndex)) + ".";
            }

            ret += metadata.GetStringFromIndex(typeDef.NameIndex);
            var names = new List<string>();
            if (typeDef.GenericContainerIndex < 0) return ret;

            var genericContainer = metadata.genericContainers[typeDef.GenericContainerIndex];
            for (var i = 0; i < genericContainer.genericParameterCount; i++)
            {
                var genericParameterIndex = genericContainer.genericParameterStart + i;
                var param = metadata.genericParameters[genericParameterIndex];
                names.Add(metadata.GetStringFromIndex(param.nameIndex));
            }

            ret = ret.Replace($"`{genericContainer.genericParameterCount}", "");
            ret += $"<{string.Join(", ", names)}>";

            return ret;
        }

        internal static Il2CppTypeReflectionData[]? GetGenericTypeParams(Il2CppGenericInst genericInst)
        {
            if (LibCpp2IlMain.Binary == null || LibCpp2IlMain.TheMetadata == null) return null;

            var types = new Il2CppTypeReflectionData[genericInst.pointerCount];
            var pointers = LibCpp2IlMain.Binary.ReadClassArrayAtVirtualAddress<ulong>(genericInst.pointerStart, (long) genericInst.pointerCount);
            for (uint i = 0; i < genericInst.pointerCount; ++i)
            {
                var oriType = LibCpp2IlMain.Binary.GetIl2CppTypeFromPointer(pointers[i]);
                types[i] = GetTypeReflectionData(oriType)!;
            }

            return types;
        }

        internal static string GetGenericTypeParamNames(Il2CppMetadata metadata, Il2CppBinary cppAssembly, Il2CppGenericInst genericInst)
        {
            var typeNames = new string[genericInst.pointerCount];
            var pointers = cppAssembly.ReadClassArrayAtVirtualAddress<ulong>(genericInst.pointerStart, (long) genericInst.pointerCount);
            for (uint i = 0; i < genericInst.pointerCount; ++i)
            {
                var oriType = cppAssembly.GetIl2CppTypeFromPointer(pointers[i]);
                typeNames[i] = GetTypeName(metadata, cppAssembly, oriType);
            }

            return $"<{string.Join(", ", typeNames)}>";
        }

        public static string GetTypeName(Il2CppMetadata metadata, Il2CppBinary cppAssembly, Il2CppType type, bool fullName = false)
        {
            string ret;
            switch (type.Type)
            {
                case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
                case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
                {
                    var typeDef = metadata.typeDefs[type.Data.ClassIndex];
                    ret = string.Empty;

                    ret += GetTypeName(metadata, cppAssembly, typeDef, fullName);
                    break;
                }
                case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
                {
                    var genericClass = cppAssembly.ReadReadableAtVirtualAddress<Il2CppGenericClass>(type.Data.GenericClass);
                    var typeDef = metadata.typeDefs[genericClass.TypeDefinitionIndex];
                    ret = metadata.GetStringFromIndex(typeDef.NameIndex);
                    var genericInst = cppAssembly.ReadReadableAtVirtualAddress<Il2CppGenericInst>(genericClass.Context.class_inst);
                    ret = ret.Replace($"`{genericInst.pointerCount}", "");
                    ret += GetGenericTypeParamNames(metadata, cppAssembly, genericInst);
                    break;
                }
                case Il2CppTypeEnum.IL2CPP_TYPE_VAR:
                case Il2CppTypeEnum.IL2CPP_TYPE_MVAR:
                {
                    var param = metadata.genericParameters[type.Data.GenericParameterIndex];
                    ret = metadata.GetStringFromIndex(param.nameIndex);
                    break;
                }
                case Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
                {
                    var arrayType = cppAssembly.ReadReadableAtVirtualAddress<Il2CppArrayType>(type.Data.Array);
                    var oriType = cppAssembly.GetIl2CppTypeFromPointer(arrayType.etype);
                    ret = $"{GetTypeName(metadata, cppAssembly, oriType)}[{new string(',', arrayType.rank - 1)}]";
                    break;
                }
                case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
                {
                    var oriType = cppAssembly.GetIl2CppTypeFromPointer(type.Data.Type);
                    ret = $"{GetTypeName(metadata, cppAssembly, oriType)}[]";
                    break;
                }
                case Il2CppTypeEnum.IL2CPP_TYPE_PTR:
                {
                    var oriType = cppAssembly.GetIl2CppTypeFromPointer(type.Data.Type);
                    ret = $"{GetTypeName(metadata, cppAssembly, oriType)}*";
                    break;
                }
                default:
                    ret = TypeString[(int) type.Type];
                    break;
            }

            return ret;
        }

        internal static object? GetDefaultValue(int dataIndex, int typeIndex)
        {
            var metadata = LibCpp2IlMain.TheMetadata!;
            var theDll = LibCpp2IlMain.Binary!;

            if (dataIndex == -1)
                return null; //Literally null.

            var pointer = metadata.GetDefaultValueFromIndex(dataIndex);
            if (pointer <= 0) return null;

            var defaultValueType = theDll.GetType(typeIndex);
            switch (defaultValueType.Type)
            {
                case Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                    return metadata.ReadClassAtRawAddr<bool>(pointer);
                case Il2CppTypeEnum.IL2CPP_TYPE_U1:
                    return metadata.ReadClassAtRawAddr<byte>(pointer);
                case Il2CppTypeEnum.IL2CPP_TYPE_I1:
                    return metadata.ReadClassAtRawAddr<sbyte>(pointer);
                case Il2CppTypeEnum.IL2CPP_TYPE_CHAR:
                    return BitConverter.ToChar(metadata.ReadByteArrayAtRawAddress(pointer, 2), 0);
                case Il2CppTypeEnum.IL2CPP_TYPE_U2:
                    return metadata.ReadClassAtRawAddr<ushort>(pointer);
                case Il2CppTypeEnum.IL2CPP_TYPE_I2:
                    return metadata.ReadClassAtRawAddr<short>(pointer);
                case Il2CppTypeEnum.IL2CPP_TYPE_U4:
                    if (LibCpp2IlMain.MetadataVersion < 29)
                        return metadata.ReadClassAtRawAddr<uint>(pointer);
                    return metadata.ReadUnityCompressedUIntAtRawAddr(pointer, out _);
                case Il2CppTypeEnum.IL2CPP_TYPE_I4:
                    if (LibCpp2IlMain.MetadataVersion < 29)
                        return metadata.ReadClassAtRawAddr<int>(pointer);
                    return metadata.ReadUnityCompressedIntAtRawAddr(pointer, out _);
                case Il2CppTypeEnum.IL2CPP_TYPE_U8:
                    return metadata.ReadClassAtRawAddr<ulong>(pointer, true);
                case Il2CppTypeEnum.IL2CPP_TYPE_I8:
                    return metadata.ReadClassAtRawAddr<long>(pointer, true);
                case Il2CppTypeEnum.IL2CPP_TYPE_R4:
                    return metadata.ReadClassAtRawAddr<float>(pointer);
                case Il2CppTypeEnum.IL2CPP_TYPE_R8:
                    return metadata.ReadClassAtRawAddr<double>(pointer);
                case Il2CppTypeEnum.IL2CPP_TYPE_STRING:
                    int len;
                    int lenLen = 4;
                    if (LibCpp2IlMain.MetadataVersion < 29)
                        len = metadata.ReadClassAtRawAddr<int>(pointer);
                    else
                        len = metadata.ReadUnityCompressedIntAtRawAddr(pointer, out lenLen);
                    if (len > 1024 * 64)
                        LibLogger.WarnNewline("[GetDefaultValue] String length is really large: " + len);
                    return Encoding.UTF8.GetString(metadata.ReadByteArrayAtRawAddress(pointer + lenLen, len));
                default:
                    return null;
            }
        }

        public static Il2CppTypeReflectionData WrapType(Il2CppTypeDefinition what)
        {
            return new Il2CppTypeReflectionData
            {
                baseType = what,
                genericParams = new Il2CppTypeReflectionData[0],
                isGenericType = false,
                isType = true,
            };
        }

        public static Il2CppTypeReflectionData GetTypeReflectionData(Il2CppType forWhat)
        {
            if (LibCpp2IlMain.Binary == null || LibCpp2IlMain.TheMetadata == null)
                throw new Exception("Can't get type reflection data when not initialized. How did you even get the type?");

            switch (forWhat.Type)
            {
                case Il2CppTypeEnum.IL2CPP_TYPE_OBJECT:
                    return WrapType(LibCpp2IlReflection.GetType("Object", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_VOID:
                    return WrapType(LibCpp2IlReflection.GetType("Void", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                    return WrapType(LibCpp2IlReflection.GetType("Boolean", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_CHAR:
                    return WrapType(LibCpp2IlReflection.GetType("Char", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_I1:
                    return WrapType(LibCpp2IlReflection.GetType("SByte", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_U1:
                    return WrapType(LibCpp2IlReflection.GetType("Byte", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_I2:
                    return WrapType(LibCpp2IlReflection.GetType("Int16", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_U2:
                    return WrapType(LibCpp2IlReflection.GetType("UInt16", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_I4:
                    return WrapType(LibCpp2IlReflection.GetType("Int32", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_U4:
                    return WrapType(LibCpp2IlReflection.GetType("UInt32", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_I:
                    return WrapType(LibCpp2IlReflection.GetType("IntPtr", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_U:
                    return WrapType(LibCpp2IlReflection.GetType("UIntPtr", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_I8:
                    return WrapType(LibCpp2IlReflection.GetType("Int64", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_U8:
                    return WrapType(LibCpp2IlReflection.GetType("UInt64", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_R4:
                    return WrapType(LibCpp2IlReflection.GetType("Single", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_R8:
                    return WrapType(LibCpp2IlReflection.GetType("Double", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_STRING:
                    return WrapType(LibCpp2IlReflection.GetType("String", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_TYPEDBYREF:
                    return WrapType(LibCpp2IlReflection.GetType("TypedReference", "System")!);
                case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
                case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
                    //"normal" type
                    return new Il2CppTypeReflectionData
                    {
                        baseType = LibCpp2IlMain.TheMetadata.typeDefs[forWhat.Data.ClassIndex],
                        genericParams = new Il2CppTypeReflectionData[0],
                        isType = true,
                        isGenericType = false,
                    };
                case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
                {
                    //Generic type
                    var genericClass = LibCpp2IlMain.Binary.ReadReadableAtVirtualAddress<Il2CppGenericClass>(forWhat.Data.GenericClass);

                    //CHANGED IN v27: typeDefinitionIndex is a ptr to the type in the file.
                    Il2CppTypeDefinition typeDefinition;
                    if (LibCpp2IlMain.MetadataVersion < 27f)
                        typeDefinition = LibCpp2IlMain.TheMetadata.typeDefs[genericClass.TypeDefinitionIndex];
                    else
                    {
                        //This is slightly annoying, because we will have already read this type, but we have to re-read it. TODO FUTURE: Make a mapping of type definition addr => type def?
                        var type = LibCpp2IlMain.Binary.ReadReadableAtVirtualAddress<Il2CppType>((ulong) genericClass.TypeDefinitionIndex);
                        typeDefinition = LibCpp2IlMain.TheMetadata!.typeDefs[type.Data.ClassIndex];
                    }

                    var genericInst = LibCpp2IlMain.Binary.ReadReadableAtVirtualAddress<Il2CppGenericInst>(genericClass.Context.class_inst);
                    var pointers = LibCpp2IlMain.Binary.GetPointers(genericInst.pointerStart, (long) genericInst.pointerCount);
                    var genericParams = pointers
                        .Select(pointer => LibCpp2IlMain.Binary.GetIl2CppTypeFromPointer(pointer))
                        .Select(type => GetTypeReflectionData(type)!) //Recursive call here
                        .ToList();

                    return new Il2CppTypeReflectionData
                    {
                        baseType = typeDefinition,
                        genericParams = genericParams.ToArray(),
                        isType = true,
                        isGenericType = true,
                    };
                }
                case Il2CppTypeEnum.IL2CPP_TYPE_VAR:
                case Il2CppTypeEnum.IL2CPP_TYPE_MVAR:
                {
                    var param = LibCpp2IlMain.TheMetadata.genericParameters[forWhat.Data.GenericParameterIndex];
                    var genericName = LibCpp2IlMain.TheMetadata.GetStringFromIndex(param.nameIndex);

                    return new Il2CppTypeReflectionData
                    {
                        baseType = null,
                        genericParams = new Il2CppTypeReflectionData[0],
                        isType = false,
                        isGenericType = false,
                        variableGenericParamName = genericName,
                        variableGenericParamIndex = forWhat.Data.GenericParameterIndex,
                    };
                }
                case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
                {
                    var oriType = LibCpp2IlMain.Binary.GetIl2CppTypeFromPointer(forWhat.Data.Type);
                    return new Il2CppTypeReflectionData
                    {
                        baseType = null,
                        arrayType = GetTypeReflectionData(oriType),
                        arrayRank = 1,
                        isArray = true,
                        isType = false,
                        isGenericType = false,
                        genericParams = new Il2CppTypeReflectionData[0]
                    };
                }
                case Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
                {
                    var arrayType = LibCpp2IlMain.Binary.ReadReadableAtVirtualAddress<Il2CppArrayType>(forWhat.Data.Array);
                    var oriType = LibCpp2IlMain.Binary.GetIl2CppTypeFromPointer(arrayType.etype);
                    return new Il2CppTypeReflectionData
                    {
                        baseType = null,
                        arrayType = GetTypeReflectionData(oriType),
                        isArray = true,
                        isType = false,
                        arrayRank = arrayType.rank,
                        isGenericType = false,
                        genericParams = new Il2CppTypeReflectionData[0]
                    };
                }
                case Il2CppTypeEnum.IL2CPP_TYPE_PTR:
                {
                    var oriType = LibCpp2IlMain.Binary.GetIl2CppTypeFromPointer(forWhat.Data.Type);
                    var ret = GetTypeReflectionData(oriType)!;
                    ret.isPointer = true;
                    return ret;
                }
            }

            throw new ArgumentException($"Unknown type {forWhat.Type}");
        }

        public static int VersionAwareSizeOf(Type type, bool dontCheckVersionAttributes = false, bool downsize = true)
        {
            if (type.IsEnum)
                type = type.GetEnumUnderlyingType();

            if (type.IsPrimitive)
                return (int) PrimitiveSizes[type.Name];

            var shouldDownsize = downsize && LibCpp2IlMain.Binary!.is32Bit;

            var size = 0;
            foreach (var field in type.GetFields())
            {
                if (!dontCheckVersionAttributes)
                {
                    if (!ShouldReadFieldOnThisVersion(field))
                        //Move to next field.
                        continue;
                }

                switch (field.FieldType.Name)
                {
                    case "Int64":
                    case "UInt64":
                        size += shouldDownsize ? 4 : 8;
                        break;
                    case "Int32":
                    case "UInt32":
                        size += 4;
                        break;
                    case "Int16":
                    case "UInt16":
                        size += 2;
                        break;
                    case "Byte":
                    case "SByte":
                        size += 1;
                        break;
                    default:
                        if (field.FieldType == type)
                            throw new Exception($"Infinite recursion is not allowed. Field {field} of type {type} has the same type as its parent.");

                        size += VersionAwareSizeOf(field.FieldType, dontCheckVersionAttributes, downsize);
                        break;
                }
            }

            return size;
        }

        internal static IEnumerable<int> Range(int start, int count)
        {
            for (var i = start; i < start + count; i++)
            {
                yield return i;
            }
        }

        internal static bool ShouldReadFieldOnThisVersion(FieldInfo i)
        {
            if (!_cachedVersionAttributes.TryGetValue(i, out var attrs))
            {
                //GetCustomAttributes is reasonably slow, so we cache here.
                attrs = Attribute.GetCustomAttributes(i, typeof(VersionAttribute)).Cast<VersionAttribute>().ToArray();
                _cachedVersionAttributes[i] = attrs;
            }

            //Either no version attribute present, or we're in one of the acceptable versions.
            return attrs.Length == 0 || attrs.Any(attr => LibCpp2IlMain.MetadataVersion >= attr.Min && LibCpp2IlMain.MetadataVersion <= attr.Max);
        }

        internal static void PopulateDeclaringAssemblyCache()
        {
            foreach (var assembly in LibCpp2IlMain.TheMetadata!.imageDefinitions)
            {
                foreach (var il2CppTypeDefinition in assembly.Types!)
                {
                    il2CppTypeDefinition.DeclaringAssembly = assembly;
                }
            }
        }
    }
}