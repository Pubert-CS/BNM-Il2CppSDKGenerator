public static class Config
{
    public const string allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890_";
    public static Dictionary<string, Tuple<string, string>> DefaultTypeMap = new()
    {
        { "System.Boolean", new("bool", "stdbool.h") },
        { "System.Int32", new("int", "cstdint") },
        { "System.Single", new("float", "cmath") },
        { "System.IntPtr", new("BNM::Types::nint", "BNM/Defaults.hpp") },
        { "System.UInt32", new("BNM::Types::uint", "BNM/Defaults.hpp") },
        { "System.Byte", new("BNM::Types::byte", "BNM/Defaults.hpp") },
        { "System.Void", new("void", "cstdint") },
        { "System.UInt64", new("uint64_t", "cstdint") },
        { "System.Int64", new("int64_t", "cstdint") },
        { "System.Int16", new("int16_t", "cstdint") },
        { "System.Char", new("uint8_t", "cstdint") },
        { "System.SByte", new("BNM::Types::sbyte", "BNM/Defaults.hpp") },
        { "System.UInt16", new("BNM::Types::ushort", "BNM/Defaults.hpp") },
        { "System.UIntPtr", new("BNM::Types::nuint", "BNM/Defaults.hpp") },
        { "System.Double", new("BNM::Types::decimal", "BNM/Defaults.hpp") },
        { "System.String", new("BNM::Structures::Mono::String*", "BNM/BasicMonoStructures.hpp") },
        { "System.Object", new("BNM::IL2CPP::Il2CppObject*", "BNM/Il2CppHeaders.hpp") },
        { "UnityEngine.Vector4", new("BNM::Structures::Unity::Vector4", "BNM/UnityStructures/Vector4.hpp") },
        { "UnityEngine.Vector3", new("BNM::Structures::Unity::Vector3", "BNM/UnityStructures/Vector3.hpp") },
        { "UnityEngine.Vector2", new("BNM::Structures::Unity::Vector2", "BNM/UnityStructures/Vector2.hpp") },
        { "UnityEngine.Quaternion", new("BNM::Structures::Unity::Quaternion", "BNM/UnityStructures/Quaternion.hpp") },
        { "UnityEngine.Rect", new("BNM::Structures::Unity::Rect", "BNM/UnityStructures/Rect.hpp") },
        { "UnityEngine.Matrix4x4", new("BNM::Structures::Unity::Matrix4x4", "BNM/UnityStructures/Matrix4x4.hpp") },
        { "UnityEngine.Matrix3x3", new("BNM::Structures::Unity::Matrix3x3", "BNM/UnityStructures/Matrix3x3.hpp") },
        { "UnityEngine.Color", new("BNM::Structures::Unity::Color", "BNM/UnityStructures/Color.hpp") },
        { "UnityEngine.RaycastHit", new("BNM::Structures::Unity::RaycastHit", "BNM/UnityStructures/RaycastHit.hpp") },
        { "UnityEngine.Ray", new("BNM::Structures::Unity::Ray", "BNM/UnityStructures/Ray.hpp") },
        { "System.Type", new("BNM::MonoType*", "BNM/Il2CppHeaders.hpp") }
    };
}