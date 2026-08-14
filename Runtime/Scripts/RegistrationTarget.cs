using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteAlways]
public class RegiTarget : MonoBehaviour
{
    [Range(3, 5)] public int amountControlPoints;
    [HideInInspector][SerializeField] public RegiMarker[] markers;
    [HideInInspector]public Vector3[] relativeMarkerPositions;
    [HideInInspector]public Guid uuidName;

    private void OnValidate()
    {
        ActivateMarkers();
    }

    private void Start()
    {
        Quaternion inverseRotation = Quaternion.Inverse(transform.rotation);
        relativeMarkerPositions = new Vector3[markers.Length];
        for (int i = 0; i < markers.Length; i++)
        {
            relativeMarkerPositions[i] =
                inverseRotation * (markers[i].transform.position - transform.position);
        }

        ActivateMarkers();
    }

    public Vector3[] GetActiveRelativeMarkerPositions()
    {
        return relativeMarkerPositions.Take(amountControlPoints).ToArray();
    }
    public List<Vector3> GetMarkerPositions()
    {
        return markers
            .Where(marker => marker.gameObject.activeSelf)
            .Select(marker => marker.transform.position)
            .ToList();
    }

    public void SetVisible(bool visible)
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer renderer in renderers)
        {
            renderer.enabled = visible;
        }
    }

    private bool StillHasMarkers()
    {
        if (markers == null || markers.Length < 5 || markers[0] == null) return false;
        return true;
    }

    private void ActivateMarkers()
    {
        if (markers == null || markers.Length == 0 || markers[0] == null) InitMarkers();
        for (int i = 0; i < markers.Length; i++)
        {
            markers[i].gameObject.SetActive(i < amountControlPoints);
        }
    }

    private void InitMarkers()
    {
        if (StillHasMarkers()) return;
        markers = new RegiMarker[5];
        for (int i = 0; i < markers.Length; i++)
        {
            GameObject go = new GameObject();
            go.name = "RegistrationPlaneProjection Marker " + i;
            go.transform.position = transform.position;
            go.transform.parent = transform;
            markers[i] = go.AddComponent<RegiMarker>();
            markers[i].color = Helper.GetColorForIndex(i);
        }
    }
}
