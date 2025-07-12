using UnityEngine;

public class ZeroRotationTerrainSpawner : MonoBehaviour
{
    public GameObject[] terrainPrefabs;
    public int numberToSpawn = 20;
    public string startPointName = "StartPoint";
    public string endPointName = "EndPoint";

    private GameObject lastSegment;

    void Start()
    {
        for (int i = 0; i < numberToSpawn; i++)
        {
            SpawnSegment(i);
        }
    }

    void SpawnSegment(int index)
    {
        GameObject prefab = terrainPrefabs[index % terrainPrefabs.Length];
        GameObject newSegment = Instantiate(prefab);

        newSegment.name = $"Terrain_{index}_{prefab.name}";
        newSegment.transform.rotation = Quaternion.identity;

        if (lastSegment == null)
        {
            newSegment.transform.position = transform.position;
        }
        else
        {
            Transform prevEnd = lastSegment.transform.Find(endPointName);
            Transform newStart = newSegment.transform.Find(startPointName);

            if (prevEnd == null || newStart == null)
            {
                Debug.LogError($"Missing StartPoint or EndPoint on {prefab.name}");
                Destroy(newSegment);
                return;
            }

            // Position new segment so its StartPoint aligns with previous EndPoint
            Vector3 offset = newStart.position - newSegment.transform.position;
            newSegment.transform.position = prevEnd.position - offset;
        }

        lastSegment = newSegment;
    }
}
