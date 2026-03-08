namespace UnityEngine.ProBuilder.Debug
{
    public class Teleport : MonoBehaviour
    {
        [SerializeField]
        Transform m_Destination;

        void OnTriggerEnter(Collider collider)
        {
            transform.position = m_Destination.position;
            var body = GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = Vector3.zero;
#else
            body.velocity = Vector3.zero;
#endif
        }
    }
}
