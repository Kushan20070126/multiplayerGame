using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ══════════════════════════════════════════════════════════════
//  ItemGripSystem.cs  — Secret Neighbor-style hand grip
//
//  SETUP:
//  1. Attach to your Player GameObject.
//  2. playerCamera  → your Camera transform.
//  3. holdPoint     → empty child of Camera at local (0.3, -0.25, 0.5)
//                     This is your "hand" anchor in front of the camera.
//  4. Every pickable object needs: Rigidbody + Collider + Tag "Pickable"
// ══════════════════════════════════════════════════════════════ 

[RequireComponent(typeof(Collider))]
public class ItemGripSystem : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────────

    [Header("References")]
    public Transform playerCamera;
    [Tooltip("Empty child of Camera — the hand anchor (e.g. local pos 0.3, -0.25, 0.5)")]
    public Transform holdPoint;

    [Header("Interaction")]
    public float     pickupRange = 2.5f;
    public LayerMask pickupMask  = ~0;

    [Header("Hand Hold")]
    [Tooltip("Fine-tune position of item inside the hand anchor")]
    public Vector3 holdPositionOffset = new Vector3(0f, 0f, 0f);
    [Tooltip("Rotation of item while held — rotate so it looks natural in hand")]
    public Vector3 holdRotationOffset = new Vector3(0f, 0f, 0f);
    [Tooltip("How fast item lerps into the hand on pickup (20=snappy, 8=floaty)")]
    public float   holdSnapSpeed      = 18f;
    [Tooltip("Subtle bob while walking so item feels physically carried")]
    public bool    enableHandBob      = true;
    public float   handBobAmount      = 0.008f;
    public float   handBobSpeed       = 8f;

    [Header("Throw")]
    public float throwForce  = 12f;
    public float throwUpward = 1.5f;

    [Header("Inventory")]
    [Tooltip("Max items carried (2 = Secret Neighbor default)")]
    public int inventorySize = 2;

    [Header("Inspect  (hold Q)")]
    public float inspectSnapDistance = 0.35f;
    public float inspectRotateSpeed  = 100f;

    [Header("Audio")]
    public AudioClip pickupSound;
    public AudioClip dropSound;
    public AudioClip throwSound;

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────

    private List<GameObject> _inventory    = new List<GameObject>();
    private int              _activeSlot   = 0;
    private GameObject       _heldItem     = null;
    private Rigidbody        _heldRb       = null;
    private bool             _isInspecting = false;
    private float            _bobTimer     = 0f;
    private AudioSource      _audio;
    private GameObject       _highlighted  = null;

    // ─────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────

    void Awake()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 0f;

        if (holdPoint    == null) Debug.LogWarning("[ItemGripSystem] holdPoint not assigned!");
        if (playerCamera == null) Debug.LogWarning("[ItemGripSystem] playerCamera not assigned!");
    }

    void Update()
    {
        HandleHighlight();
        HandlePickupInput();
        HandleSlotSwitch();
        HandleThrow();
        HandleDrop();
        HandleInspect();
        UpdateHandHold();
    }

    // ─────────────────────────────────────────────
    //  HIGHLIGHT — emission pulse on nearby item
    // ─────────────────────────────────────────────

    private void HandleHighlight()
    {
        if (playerCamera == null) return;
        Ray ray = new Ray(playerCamera.position, playerCamera.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupMask)
            && hit.collider.CompareTag("Pickable")
            && hit.collider.gameObject != _heldItem)
        {
            if (_highlighted != hit.collider.gameObject)
            {
                SetHighlight(_highlighted, false);
                _highlighted = hit.collider.gameObject;
                SetHighlight(_highlighted, true);
            }
            return;
        }
        SetHighlight(_highlighted, false);
        _highlighted = null;
    }

    private void SetHighlight(GameObject obj, bool on)
    {
        if (obj == null) return;
        // Swap with your outline shader call, e.g.:
        //   obj.GetComponent<Outline>().enabled = on;
        Renderer r = obj.GetComponent<Renderer>();
        if (r == null) return;
        if (on) r.material.EnableKeyword("_EMISSION");
        else    r.material.DisableKeyword("_EMISSION");
    }

    // ─────────────────────────────────────────────
    //  PICKUP  (E key)
    // ─────────────────────────────────────────────

    private void HandlePickupInput()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;
        if (_inventory.Count >= inventorySize) return;
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupMask)) return;
        if (!hit.collider.CompareTag("Pickable")) return;

        PickupItem(hit.collider.gameObject);
    }

    // ─────────────────────────────────────────────
    //  PICKUP ITEM — parent directly to hand
    // ─────────────────────────────────────────────

    private void PickupItem(GameObject item)
    {
        if (_inventory.Contains(item)) return;

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"[ItemGripSystem] {item.name} has no Rigidbody — add one!");
            return;
        }

        // ── 1. Fully freeze physics ──────────────
        rb.isKinematic     = true;
        rb.useGravity      = false;
        rb.velocity        = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // ── 2. Disable colliders so item doesn't
        //       push walls while held ──────────────
        foreach (var col in item.GetComponentsInChildren<Collider>())
            col.enabled = false;

        // ── 3. Parent to holdPoint (the hand) ────
        item.transform.SetParent(holdPoint);

        // Start from slightly behind so it lerps forward
        item.transform.localPosition = holdPositionOffset + new Vector3(0f, 0f, -0.3f);
        item.transform.localRotation = Quaternion.Euler(holdRotationOffset);

        // ── 4. Track state ───────────────────────
        _heldItem = item;
        _heldRb   = rb;
        _inventory.Add(item);
        _activeSlot = _inventory.IndexOf(item);

        PlaySound(pickupSound);
        Debug.Log($"[ItemGripSystem] Picked up: {item.name} ({_inventory.Count}/{inventorySize})");
    }

    // ─────────────────────────────────────────────
    //  HAND HOLD UPDATE — lerp to hand position
    //  Called every frame while item is held.
    // ─────────────────────────────────────────────

    private void UpdateHandHold()
    {
        if (_heldItem == null || _isInspecting) return;

        // Base target is the offset inside holdPoint
        Vector3 targetLocal = holdPositionOffset;

        // Optional subtle bob when walking
        if (enableHandBob)
        {
            CharacterController cc = GetComponent<CharacterController>();
            Vector3 flatVel = cc != null
                ? new Vector3(cc.velocity.x, 0f, cc.velocity.z)
                : Vector3.zero;

            if (flatVel.magnitude > 0.2f)
                _bobTimer += Time.deltaTime * handBobSpeed;
            else
                _bobTimer = Mathf.Lerp(_bobTimer, 0f, Time.deltaTime * 6f);

            targetLocal += new Vector3(
                Mathf.Sin(_bobTimer * 0.5f) * handBobAmount,
                Mathf.Sin(_bobTimer)        * handBobAmount,
                0f
            );
        }

        // Smooth lerp position into hand
        _heldItem.transform.localPosition = Vector3.Lerp(
            _heldItem.transform.localPosition,
            targetLocal,
            Time.deltaTime * holdSnapSpeed
        );

        // Smooth lerp rotation to held orientation
        _heldItem.transform.localRotation = Quaternion.Slerp(
            _heldItem.transform.localRotation,
            Quaternion.Euler(holdRotationOffset),
            Time.deltaTime * holdSnapSpeed
        );
    }

    // ─────────────────────────────────────────────
    //  SLOT SWITCH  (scroll wheel / 1-4 keys)
    // ─────────────────────────────────────────────

    private void HandleSlotSwitch()
    {
        if (_inventory.Count <= 1) return;

        int newSlot = _activeSlot;
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) newSlot--;
        if (scroll < 0f) newSlot++;

        for (int i = 0; i < Mathf.Min(_inventory.Count, 4); i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) newSlot = i;

        newSlot = Mathf.Clamp(newSlot, 0, _inventory.Count - 1);
        if (newSlot == _activeSlot) return;

        StashCurrent();
        _activeSlot = newSlot;
        GripFromInventory(_activeSlot);
    }

    private void StashCurrent()
    {
        if (_heldItem == null) return;
        _heldItem.SetActive(false);
        _heldItem = null;
        _heldRb   = null;
    }

    private void GripFromInventory(int slot)
    {
        if (slot >= _inventory.Count) return;
        GameObject item = _inventory[slot];
        if (item == null) { _inventory.RemoveAt(slot); return; }

        item.SetActive(true);
        item.transform.SetParent(holdPoint);
        item.transform.localPosition = holdPositionOffset;
        item.transform.localRotation = Quaternion.Euler(holdRotationOffset);

        _heldItem = item;
        _heldRb   = item.GetComponent<Rigidbody>();
    }

    // ─────────────────────────────────────────────
    //  THROW  (RMB or F)
    // ─────────────────────────────────────────────

    private void HandleThrow()
    {
        if (!Input.GetMouseButtonDown(1) && !Input.GetKeyDown(KeyCode.F)) return;
        if (_heldItem == null) return;
        ReleaseItem(true);
    }

    // ─────────────────────────────────────────────
    //  DROP  (G)
    // ─────────────────────────────────────────────

    private void HandleDrop()
    {
        if (!Input.GetKeyDown(KeyCode.G)) return;
        if (_heldItem == null) return;
        ReleaseItem(false);
    }

    // ─────────────────────────────────────────────
    //  RELEASE ITEM
    // ─────────────────────────────────────────────

    private void ReleaseItem(bool isThrow)
    {
        if (_heldItem == null) return;

        GameObject item = _heldItem;
        Rigidbody  rb   = _heldRb;

        // ── Un-parent and restore physics ────────
        item.transform.SetParent(null);
        rb.isKinematic = false;
        rb.useGravity  = true;

        // ── Re-enable colliders ───────────────────
        foreach (var col in item.GetComponentsInChildren<Collider>())
            col.enabled = true;

        if (isThrow)
        {
            Vector3 dir = (playerCamera.forward + playerCamera.up * throwUpward).normalized;
            rb.velocity = Vector3.zero;
            rb.AddForce(dir * throwForce, ForceMode.Impulse);
            PlaySound(throwSound);
        }
        else
        {
            PlaySound(dropSound);
        }

        _inventory.Remove(item);
        _heldItem     = null;
        _heldRb       = null;
        _isInspecting = false;

        // Auto-grip next stashed item if any
        if (_inventory.Count > 0)
        {
            _activeSlot = Mathf.Clamp(_activeSlot, 0, _inventory.Count - 1);
            GripFromInventory(_activeSlot);
        }
    }

    // ─────────────────────────────────────────────
    //  INSPECT  (hold Q)
    // ─────────────────────────────────────────────

    private void HandleInspect()
    {
        if (_heldItem == null) return;

        if (Input.GetKey(KeyCode.Q))
        {
            _isInspecting = true;
            _heldItem.transform.localPosition = Vector3.Lerp(
                _heldItem.transform.localPosition,
                new Vector3(0f, 0f, inspectSnapDistance),
                Time.deltaTime * 12f
            );
            float mx = Input.GetAxis("Mouse X") * inspectRotateSpeed * Time.deltaTime;
            float my = Input.GetAxis("Mouse Y") * inspectRotateSpeed * Time.deltaTime;
            _heldItem.transform.Rotate(playerCamera.up,    -mx, Space.World);
            _heldItem.transform.Rotate(playerCamera.right,  my, Space.World);
        }
        else
        {
            _isInspecting = false;
        }
    }

    // ─────────────────────────────────────────────
    //  AUDIO
    // ─────────────────────────────────────────────

    private void PlaySound(AudioClip clip)
    {
        if (_audio != null && clip != null)
            _audio.PlayOneShot(clip);
    }

    // ─────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────

    public GameObject                HeldItem      => _heldItem;
    public IReadOnlyList<GameObject> Inventory     => _inventory.AsReadOnly();
    public float                     InventoryFill => (float)_inventory.Count / inventorySize;
    public bool                      IsInspecting  => _isInspecting;

    /// <summary>Drop everything instantly — call when Neighbor catches the player.</summary>
    public void ForceDropAll()
    {
        if (_heldItem != null) ReleaseItem(false);
        foreach (var item in new List<GameObject>(_inventory))
        {
            if (item == null) continue;
            item.SetActive(true);
            item.transform.SetParent(null);
            foreach (var col in item.GetComponentsInChildren<Collider>())
                col.enabled = true;
            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb) { rb.isKinematic = false; rb.useGravity = true; }
        }
        _inventory.Clear();
        _activeSlot = 0;
    }
}