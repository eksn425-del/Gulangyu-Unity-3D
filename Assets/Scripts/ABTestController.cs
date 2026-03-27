using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Networking;
using System.Text;

public class ABTestController : MonoBehaviour
{
    public GameObject agentPrefab;
    public GameObject agentPrefabA;
    public GameObject agentPrefabB;
    public Transform spawnA;
    public Transform spawnB;
    public Transform targetA;
    public Transform targetB;

    public int groupACount = 3;
    public int groupBCount = 3;
    public float groupASpeed = 3.5f;
    public float groupBSpeed = 3.0f;
    public float spawnSpacing = 1.2f;
    public bool autoStartOnPlay = true;
    public bool autoSubmitSummary = true;
    public float maxTestDuration = 120f;
    public string abSummaryUrl = "http://127.0.0.1:8000/api/ab_test_summary";
    public string summaryClientSource = "unity";
    public bool useBackendRouteForGroupB = true;
    public string groupBRouteApiBaseUrl = "http://127.0.0.1:8000";
    public string bGroupStartNodeId = "";
    public string bGroupEndNodeId = "";
    public string groupBUserType = "blind";
    public string groupBStrategy = "safest";
    public bool overrideDigitalCaneScanSettings = true;
    public bool enableForwardHazardScanForABAgents = true;
    public bool enableDropOffScanForABAgents = false;

    private readonly List<GameObject> spawnedAgents = new List<GameObject>();
    private readonly List<AgentRuntime> runtimeAgents = new List<AgentRuntime>();
    private Coroutine summaryCoroutine;

    void Start()
    {
        if (autoStartOnPlay)
        {
            StartABTest();
        }
    }

    [ContextMenu("Start AB Test")]
    public void StartABTest()
    {
        ClearSpawnedAgents();
        SpawnGroup(agentPrefabA, "group_A", groupACount, spawnA, targetA, groupASpeed, Vector3.right);
        SpawnGroup(agentPrefabB, "group_B", groupBCount, spawnB, targetB, groupBSpeed, Vector3.left);
        if (Application.isPlaying && autoSubmitSummary && runtimeAgents.Count > 0)
        {
            string testRunId = "ab_unity_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            summaryCoroutine = StartCoroutine(RunAndSubmitSummary(testRunId));
        }
    }

    [ContextMenu("Clear Spawned Agents")]
    public void ClearSpawnedAgents()
    {
        if (summaryCoroutine != null)
        {
            StopCoroutine(summaryCoroutine);
            summaryCoroutine = null;
        }

        for (int i = 0; i < spawnedAgents.Count; i++)
        {
            if (spawnedAgents[i] != null)
            {
                Destroy(spawnedAgents[i]);
            }
        }

        spawnedAgents.Clear();
        runtimeAgents.Clear();
    }

    private void SpawnGroup(GameObject groupPrefab, string groupName, int count, Transform spawnPoint, Transform targetPoint, float speed, Vector3 spacingDirection)
    {
        GameObject prefabToUse = groupPrefab != null ? groupPrefab : agentPrefab;

        if (prefabToUse == null || spawnPoint == null || targetPoint == null || count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = spawnPoint.position + spacingDirection * (i * spawnSpacing);
            Quaternion spawnRot = spawnPoint.rotation;

            GameObject agent = Instantiate(prefabToUse, spawnPos, spawnRot);
            agent.name = groupName + "_agent_" + (i + 1);

            DigitalCane cane = agent.GetComponent<DigitalCane>();
            if (cane != null)
            {
                cane.agentGroup = groupName;
                if (overrideDigitalCaneScanSettings)
                {
                    cane.enableForwardHazardScan = enableForwardHazardScanForABAgents;
                    cane.enableDropOffScan = enableDropOffScanForABAgents;
                }
            }

            PlayerController controller = agent.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.autoNavMode = true;
            }

            Rigidbody rb = agent.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            NavMeshAgent nav = agent.GetComponent<NavMeshAgent>();
            RouteFollower routeFollower = agent.GetComponent<RouteFollower>();
            bool useRouteFollower = groupName == "group_B" && useBackendRouteForGroupB && routeFollower != null;
            if (nav != null)
            {
                nav.speed = speed;
                if (useRouteFollower)
                {
                    routeFollower.routeApiBaseUrl = groupBRouteApiBaseUrl;
                    routeFollower.userType = groupBUserType;
                    routeFollower.strategy = groupBStrategy;
                    routeFollower.fallbackTarget = targetPoint;
                    routeFollower.ConfigureFallback(targetPoint.position);
                    routeFollower.StartRoute(bGroupStartNodeId, bGroupEndNodeId);
                }
                else
                {
                    nav.SetDestination(targetPoint.position);
                }
            }
            else if (controller != null)
            {
                controller.SetDestination(targetPoint.position);
            }

            spawnedAgents.Add(agent);
            runtimeAgents.Add(new AgentRuntime
            {
                groupName = groupName,
                agent = agent,
                target = targetPoint,
                nav = nav,
                routeFollower = routeFollower,
                cane = cane,
                lastPosition = agent.transform.position,
                pathLength = 0f,
                startTime = Time.time,
                completed = false,
                completionTime = 0f
            });
        }
    }

    private IEnumerator RunAndSubmitSummary(string testRunId)
    {
        float startedAt = Time.time;
        while (Time.time - startedAt < maxTestDuration)
        {
            bool allCompleted = true;
            for (int i = 0; i < runtimeAgents.Count; i++)
            {
                AgentRuntime runtime = runtimeAgents[i];
                if (runtime.completed)
                {
                    continue;
                }

                UpdatePathLength(runtime);
                if (IsArrived(runtime))
                {
                    runtime.completed = true;
                    runtime.completionTime = Time.time - runtime.startTime;
                }
                else
                {
                    allCompleted = false;
                }
            }

            if (allCompleted)
            {
                break;
            }

            yield return null;
        }

        float elapsed = Time.time - startedAt;
        for (int i = 0; i < runtimeAgents.Count; i++)
        {
            AgentRuntime runtime = runtimeAgents[i];
            UpdatePathLength(runtime);
            if (!runtime.completed)
            {
                runtime.completed = true;
                runtime.completionTime = elapsed;
            }
        }

        ABGroupMetricsPayload groupA = BuildGroupMetrics("group_A");
        ABGroupMetricsPayload groupB = BuildGroupMetrics("group_B");
        ABTestSummaryPayload payload = new ABTestSummaryPayload
        {
            test_run_id = testRunId,
            total_agents = runtimeAgents.Count,
            group_a = groupA,
            group_b = groupB,
            client_source = summaryClientSource
        };

        yield return StartCoroutine(PostSummary(payload));
        summaryCoroutine = null;
    }

    private void UpdatePathLength(AgentRuntime runtime)
    {
        if (runtime.agent == null)
        {
            return;
        }

        Vector3 current = runtime.agent.transform.position;
        runtime.pathLength += Vector3.Distance(runtime.lastPosition, current);
        runtime.lastPosition = current;
    }

    private bool IsArrived(AgentRuntime runtime)
    {
        if (runtime.agent == null)
        {
            return true;
        }

        if (runtime.nav != null)
        {
            if (runtime.routeFollower != null && !runtime.routeFollower.HasBegunNavigation)
            {
                return false;
            }

            if (runtime.nav.pathPending)
            {
                return false;
            }

            if (runtime.nav.remainingDistance > runtime.nav.stoppingDistance + 0.05f)
            {
                return false;
            }

            if (runtime.nav.hasPath && runtime.nav.velocity.sqrMagnitude > 0.01f)
            {
                return false;
            }

            return true;
        }

        if (runtime.target == null)
        {
            return true;
        }

        return Vector3.Distance(runtime.agent.transform.position, runtime.target.position) <= 0.8f;
    }

    private ABGroupMetricsPayload BuildGroupMetrics(string groupName)
    {
        int count = 0;
        int hazardCount = 0;
        float pathLengthSum = 0f;
        float completionTimeSum = 0f;

        for (int i = 0; i < runtimeAgents.Count; i++)
        {
            AgentRuntime runtime = runtimeAgents[i];
            if (runtime.groupName != groupName)
            {
                continue;
            }

            count++;
            if (runtime.cane != null)
            {
                hazardCount += runtime.cane.hazardTriggerCount;
            }

            pathLengthSum += runtime.pathLength;
            completionTimeSum += runtime.completionTime;
        }

        return new ABGroupMetricsPayload
        {
            agent_count = count,
            hazard_trigger_count = hazardCount,
            avg_path_length = count > 0 ? pathLengthSum / count : 0f,
            avg_completion_time = count > 0 ? completionTimeSum / count : 0f
        };
    }

    private IEnumerator PostSummary(ABTestSummaryPayload payload)
    {
        string json = JsonUtility.ToJson(payload);
        using (UnityWebRequest request = new UnityWebRequest(abSummaryUrl, UnityWebRequest.kHttpVerbPOST))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("[ABTestController] summary submit failed: " + request.error);
            }
            else
            {
                Debug.Log("[ABTestController] summary submit success: " + request.downloadHandler.text);
            }
        }
    }

    [System.Serializable]
    private class ABGroupMetricsPayload
    {
        public int agent_count;
        public int hazard_trigger_count;
        public float avg_path_length;
        public float avg_completion_time;
    }

    [System.Serializable]
    private class ABTestSummaryPayload
    {
        public string test_run_id;
        public int total_agents;
        public ABGroupMetricsPayload group_a;
        public ABGroupMetricsPayload group_b;
        public string client_source;
    }

    private class AgentRuntime
    {
        public string groupName;
        public GameObject agent;
        public Transform target;
        public NavMeshAgent nav;
        public RouteFollower routeFollower;
        public DigitalCane cane;
        public Vector3 lastPosition;
        public float pathLength;
        public float startTime;
        public bool completed;
        public float completionTime;
    }
}
