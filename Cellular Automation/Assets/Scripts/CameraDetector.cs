using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraDetector : MonoBehaviour
{
    new private Camera camera;
    Plane[] cameraFrustrum;
    public Bounds bounds;
    public bool visible = false;

    private void Start()
    {
        camera = GetComponent<Camera>();
    }

    public void UpdateFrustrum()
    {
        cameraFrustrum = GeometryUtility.CalculateFrustumPlanes(camera);
    }

    private void Update()
    {
        //UpdateFrustrum();

        //visible = BoundsIsVisible(bounds);
    }

    public bool BoundsIsVisible(Bounds bounds)
    {
        return GeometryUtility.TestPlanesAABB(cameraFrustrum, bounds);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
