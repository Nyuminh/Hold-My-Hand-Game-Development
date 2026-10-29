using UnityEngine;

/// <summary>
/// Controller for Front Gate (Portcullis) and Rear Gate (Double Doors) of Castle_Fortress_v2.
/// Works with Animator Controller driven by baked animations from Castle_Fortress_v2.blend.
/// </summary>
public class CastleGateController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Gate States")]
    [SerializeField] private bool frontGateOpen = false;
    [SerializeField] private bool rearGateOpen = false;

    [Header("Automatic Trigger Radius (Optional)")]
    [Tooltip("If greater than 0, gate will auto-open when Player is within this distance")]
    [SerializeField] private float frontTriggerDistance = 8f;
    [SerializeField] private float rearTriggerDistance = 8f;
    [SerializeField] private Transform frontGatePosition;
    [SerializeField] private Transform rearGatePosition;

    private Transform playerTransform;

    public bool IsFrontGateOpen => frontGateOpen;
    public bool IsRearGateOpen => rearGateOpen;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (frontGatePosition == null)
        {
            var fg = transform.Find("Front_Harrow_Portcullis");
            if (fg != null) frontGatePosition = fg;
        }

        if (rearGatePosition == null)
        {
            var rg = transform.Find("Rear_Gatehouse_Assembly");
            if (rg != null) rearGatePosition = rg;
        }
    }

    private void Start()
    {
        // Apply initial state
        UpdateAnimator();

        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void Update()
    {
        // Lazy find player if not yet found
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        // Distance-based auto opening if enabled
        if (playerTransform != null)
        {
            if (frontGatePosition != null && frontTriggerDistance > 0f)
            {
                float dist = Vector3.Distance(playerTransform.position, frontGatePosition.position);
                bool shouldOpen = dist <= frontTriggerDistance;
                if (shouldOpen != frontGateOpen)
                {
                    SetFrontGate(shouldOpen);
                }
            }

            if (rearGatePosition != null && rearTriggerDistance > 0f)
            {
                float dist = Vector3.Distance(playerTransform.position, rearGatePosition.position);
                bool shouldOpen = dist <= rearTriggerDistance;
                if (shouldOpen != rearGateOpen)
                {
                    SetRearGate(shouldOpen);
                }
            }
        }
    }

    public void ToggleFrontGate()
    {
        SetFrontGate(!frontGateOpen);
    }

    public void ToggleRearGate()
    {
        SetRearGate(!rearGateOpen);
    }

    public void SetFrontGate(bool open)
    {
        frontGateOpen = open;
        UpdateAnimator();
    }

    public void SetRearGate(bool open)
    {
        rearGateOpen = open;
        UpdateAnimator();
    }

    public void PlayFullSequence()
    {
        if (animator != null)
        {
            animator.SetTrigger("PlayFullDemo");
        }
    }

    private void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetBool("isFrontOpen", frontGateOpen);
            animator.SetBool("isRearOpen", rearGateOpen);
        }
    }

    private void OnValidate()
    {
        UpdateAnimator();
    }
}
