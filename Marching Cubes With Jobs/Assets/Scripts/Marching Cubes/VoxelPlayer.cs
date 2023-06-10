using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelPlayer : MonoBehaviour
{
    [HideInInspector]
    public CharacterController charController;

    public float speed;
    public float gravity = 9.8f;
    public bool gravityEnabled = true;

    [Header("Rotation Settings")]
    [Tooltip("X = Change in mouse position.\nY = Multiplicative factor for camera rotation.")]
    public AnimationCurve mouseSensitivityCurve = new AnimationCurve(new Keyframe(0f, 0.5f, 0f, 5f), new Keyframe(1f, 2.5f, 0f, 0f));

    [Tooltip("Time it takes to interpolate camera rotation 99% of the way to the target."), Range(0.001f, 1f)]
    public float rotationLerpTime = 0.01f;

    [Tooltip("Whether or not to invert our Y axis for mouse input to rotation.")]
    public bool invertY = false;

    public Camera cam;

    public InfiniteMarchingCubes infiniteMarchingCubes;
    private float lastMineTime = 0;
    public float mineSpeed = 0.5f;
    public float reachDistance = 10f;
    public string buildVoxelID = "base:solid";

    public Transform cursorTrans;
    private float lastCursorTime = 0;
    private float cursorUpdateSpeed = 0.1f;

    private void Start()
    {
        charController = GetComponent<CharacterController>();
    }

    private void Update()
    {

        Move();

        RotateCamera();

        if (Input.GetMouseButton(0))
        {
            Mine();
        }
        else if (Input.GetMouseButtonDown(1))
        {
            Build();
        }

        UpdateCursorPos();
    }

    private void Move()
    {
        Vector3 dir = cam.transform.right * Input.GetAxisRaw("Horizontal") + cam.transform.forward * Input.GetAxisRaw("Vertical");
        float boost = 1;
        // new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));

        if (Input.GetKey(KeyCode.R))
        {
            boost = 4;
        }

        charController.Move(dir * speed * boost * Time.deltaTime);

        if (gravityEnabled)
            charController.Move(Vector3.down * gravity * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            charController.Move(Vector3.up * speed * boost);
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            gravityEnabled = !gravityEnabled;
        }
    }

    private void RotateCamera()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Rotation
        if (Cursor.lockState == CursorLockMode.Locked)//(Input.GetMouseButton(1))
        {
            var mouseMovement = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y") * (invertY ? 1 : -1));

            var mouseSensitivityFactor = mouseSensitivityCurve.Evaluate(mouseMovement.magnitude);

            cam.transform.eulerAngles += new Vector3(mouseMovement.y * mouseSensitivityFactor, mouseMovement.x * mouseSensitivityFactor, 0);

        }

    }
    private void Mine()
    {
        if (Time.time > lastMineTime + mineSpeed)
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, reachDistance))
            {
                Vector3 hitCenter = hit.point - hit.normal * 0.5f;

                //infiniteTerrain.SetVoxel(hitCenter, "base:air");
            }

            lastMineTime = Time.time;
        }
    }

    private void Build()
    {
        if (Time.time > lastMineTime + mineSpeed)
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, reachDistance))
            {
                Vector3 hitCenter = hit.point + hit.normal * 0.5f;

                //infiniteTerrain.SetVoxel(hitCenter, buildVoxelID);
            }

            lastMineTime = Time.time;
        }
    }

    private void UpdateCursorPos()
    {
        if (Time.time > lastCursorTime + mineSpeed)
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, reachDistance))
            {
                Vector3 hitCenter = hit.point + hit.normal * 0.5f;

                //cursorTrans.position = (Vector3)infiniteTerrain.ToWorldPosition(hitCenter) * infiniteTerrain.voxelScale + Vector3.one / 2f;

                cursorTrans.position = hitCenter;
            }

            lastCursorTime = Time.time;
        }
    }
}
