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

    [Header("Rear Door Objects")]
    [SerializeField] private Transform rearDoorInnerLeft;
    [SerializeField] private Transform rearDoorInnerRight;
    [SerializeField] private Transform rearDoorOuter;
    [SerializeField] private float rearDoorOpenDuration = 1.8f;
    private Coroutine rearDoorCoroutine;

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

        if (rearDoorInnerLeft == null)
            rearDoorInnerLeft = transform.Find("Rear_Gatehouse_Assembly/Rear_Door_Inner_Left");
        if (rearDoorInnerRight == null)
            rearDoorInnerRight = transform.Find("Rear_Gatehouse_Assembly/Rear_Door_Inner_Right");
        if (rearDoorOuter == null)
            rearDoorOuter = transform.Find("Rear_Gatehouse_Assembly/Rear_Door_Outer");
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

        if (rearDoorCoroutine != null)
            StopCoroutine(rearDoorCoroutine);
        rearDoorCoroutine = StartCoroutine(AnimateRearDoors(open, rearDoorOpenDuration));
    }

    private System.Collections.IEnumerator AnimateRearDoors(bool open, float duration)
    {
        // Target Euler rotations (Z axis rotation in local coordinates)
        Quaternion targetLeft = open ? Quaternion.Euler(0f, 0f, 275.2f) : Quaternion.identity;
        Quaternion targetRight = open ? Quaternion.Euler(0f, 0f, 84.8f) : Quaternion.identity;
        Quaternion targetOuter = open ? Quaternion.Euler(0f, 0f, 275.2f) : Quaternion.identity;

        Quaternion startLeft = rearDoorInnerLeft != null ? rearDoorInnerLeft.localRotation : Quaternion.identity;
        Quaternion startRight = rearDoorInnerRight != null ? rearDoorInnerRight.localRotation : Quaternion.identity;
        Quaternion startOuter = rearDoorOuter != null ? rearDoorOuter.localRotation : Quaternion.identity;

        // When opening, convert colliders to trigger so players can walk through unobstructed
        if (open)
        {
            SetDoorCollidersTrigger(true);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            if (rearDoorInnerLeft != null)
                rearDoorInnerLeft.localRotation = Quaternion.Slerp(startLeft, targetLeft, t);
            if (rearDoorInnerRight != null)
                rearDoorInnerRight.localRotation = Quaternion.Slerp(startRight, targetRight, t);
            if (rearDoorOuter != null)
                rearDoorOuter.localRotation = Quaternion.Slerp(startOuter, targetOuter, t);

            yield return null;
        }

        if (rearDoorInnerLeft != null) rearDoorInnerLeft.localRotation = targetLeft;
        if (rearDoorInnerRight != null) rearDoorInnerRight.localRotation = targetRight;
        if (rearDoorOuter != null) rearDoorOuter.localRotation = targetOuter;

        if (!open)
        {
            SetDoorCollidersTrigger(false);
        }

        rearDoorCoroutine = null;
    }

    private void SetDoorCollidersTrigger(bool isTrigger)
    {
        Transform[] doors = new Transform[] { rearDoorInnerLeft, rearDoorInnerRight, rearDoorOuter };
        foreach (var door in doors)
        {
            if (door != null)
            {
                foreach (var col in door.GetComponentsInChildren<Collider>())
                {
                    col.isTrigger = isTrigger;
                }
            }
        }
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
