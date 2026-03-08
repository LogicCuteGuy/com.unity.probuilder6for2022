#if !UNITY_6000_0_OR_NEWER
namespace UnityEditor.Actions
{
    public enum EditorActionResult
    {
        Success,
        Canceled
    }

    public abstract class EditorAction
    {
        protected void Finish(EditorActionResult result)
        {
        }

        public static void Start(EditorAction action)
        {
            // Constructors of action instances execute before this call site,
            // so pre-6000 fallback does not require extra dispatch.
        }
    }
}
#endif
