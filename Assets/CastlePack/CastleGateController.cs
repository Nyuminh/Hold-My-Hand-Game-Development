using UnityEngine;

/// <summary>
/// Controller for Front Gate (Portcullis) and Rear Gate (Double Doors) of Castle_Fortress_v2.
/// Works with Animator Controller driven by baked animations from Castle_Fortress_v2.blend.
/// </summary>
public class CastleGateController : MonoBehaviour, Blocks.Gameplay.Core.ILevelOneGateDriver
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
    private bool climaxControlled;
    private Transform[] climaxGates;
    private Vector3[] closedGatePositions;

    public void BeginClimaxControl()
    {
        if (climaxControlled) return;
        climaxControlled = true;
        if (animator != null) animator.enabled = false;
        var gates = new System.Collections.Generic.List<Transform>();
        foreach (var child in GetComponentsInChildren<Transform>(true))
            if (child.name == "Front_Harrow_Portcullis" || child.name == "Back_Harrow_Portcullis") gates.Add(child);
        climaxGates = gates.ToArray();
        closedGatePositions = new Vector3[climaxGates.Length];
        for (int i = 0; i < climaxGates.Length; i++)
        {
            closedGatePositions[i] = climaxGates[i].position;
            // Climax supplies explicit solid blockers with a narrow child drain beside them.
            foreach (var col in climaxGates[i].GetComponentsInChildren<Collider>()) col.enabled = false;
        }
    }

    public void SetClimaxLift(float metres)
    {
        BeginClimaxControl();
        for (int i = 0; i < climaxGates.Length; i++)
            climaxGates[i].position = closedGatePositions[i] + Vector3.up * Mathf.Max(0f, metres);
        frontGateOpen = metres > 2f;
    }

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
        if (climaxControlled) return;
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
        if (climaxControlled) return;
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
