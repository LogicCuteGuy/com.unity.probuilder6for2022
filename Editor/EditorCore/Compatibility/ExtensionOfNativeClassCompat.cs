#if !UNITY_6000_0_OR_NEWER
namespace UnityEngine
{
    // Unity 2022 exposes this attribute as internal in some API profiles.
    // Provide a compatible public definition so editor classes can be tagged.
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = true)]
    public sealed class ExtensionOfNativeClassAttribute : System.Attribute
    {
    }
}
#endif
