using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Networking;

[RequireComponent(typeof(NavMeshAgent))]
public class RouteFollower : MonoBehaviour
{
    public string routeApiBaseUrl = "http://127.0.0.1:8000";
    public string userType = "blind";
    public string strategy = "safest";
    public float waypointReachDistance = 0.8f;
    public float requestTimeoutSeconds = 10f;
    public Transform fallbackTarget;

    private NavMeshAgent navAgent;
    private Coroutine routeCoroutine;
    private readonly List<Transform> activeWaypoints = new List<Transform>();
    private Vector3 fallbackDestination;
    private bool hasFallbackDestination;
    public bool HasBegunNavigation { get; private set; }

    [Serializable]
    private class RouteResponse
    {
        public bool success;
        public RouteResult[] routes;
        public string error;
    }

    [Serializable]
    private class RouteResult
    {
        public RouteStep[] steps;
    }

    [Serializable]
    private class RouteStep
    {
        public string to_name;
    }

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
    }

    public void ConfigureFallback(Vector3 destination)
    {
        fallbackDestination = destination;
        hasFallbackDestination = true;
    }

    public void StartRoute(string startNodeId, string endNodeId)
    {
        if (routeCoroutine != null)
        {
            StopCoroutine(routeCoroutine);
        }

        HasBegunNavigation = false;
        routeCoroutine = StartCoroutine(FetchAndFollowRoute(startNodeId, endNodeId));
    }

    private IEnumerator FetchAndFollowRoute(string startNodeId, string endNodeId)
    {
        activeWaypoints.Clear();

        if (string.IsNullOrWhiteSpace(startNodeId) || string.IsNullOrWhiteSpace(endNodeId))
        {
            MoveToFallback();
            routeCoroutine = null;
            yield break;
        }

        string url = string.Format(
            "{0}/api/route?start={1}&end={2}&user_type={3}&strategy={4}",
            routeApiBaseUrl.TrimEnd('/'),
            UnityWebRequest.EscapeURL(startNodeId),
            UnityWebRequest.EscapeURL(endNodeId),
            UnityWebRequest.EscapeURL(userType),
            UnityWebRequest.EscapeURL(strategy)
        );

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = Mathf.CeilToInt(requestTimeoutSeconds);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogWarning("[RouteFollower] route request failed: " + request.error);
                MoveToFallback();
                routeCoroutine = null;
                yield break;
            }

            RouteResponse response = JsonUtility.FromJson<RouteResponse>(request.downloadHandler.text);
            if (response == null || !response.success || response.routes == null || response.routes.Length == 0 || response.routes[0].steps == null || response.routes[0].steps.Length == 0)
            {
                Debug.LogWarning("[RouteFollower] route response missing steps.");
                MoveToFallback();
                routeCoroutine = null;
                yield break;
            }

            RouteStep[] steps = response.routes[0].steps;
            for (int i = 0; i < steps.Length; i++)
            {
                string targetName = steps[i].to_name;
                if (string.IsNullOrWhiteSpace(targetName))
                {
                    continue;
                }

                Transform waypoint = FindSceneTransform(targetName);
                if (waypoint != null)
                {
                    activeWaypoints.Add(waypoint);
                }
                else
                {
                    Debug.LogWarning("[RouteFollower] missing route marker: " + targetName);
                }
            }
        }

        if (activeWaypoints.Count == 0)
        {
            MoveToFallback();
            routeCoroutine = null;
            yield break;
        }

        for (int i = 0; i < activeWaypoints.Count; i++)
        {
            Transform waypoint = activeWaypoints[i];
            if (waypoint == null)
            {
                continue;
            }

            HasBegunNavigation = true;
            navAgent.SetDestination(waypoint.position);
            while (navAgent.pathPending || navAgent.remainingDistance > waypointReachDistance)
            {
                yield return null;
            }
        }

        MoveToFallback();
        routeCoroutine = null;
    }

    private void MoveToFallback()
    {
        if (navAgent == null)
        {
            return;
        }

        if (fallbackTarget != null)
        {
            HasBegunNavigation = true;
            navAgent.SetDestination(fallbackTarget.position);
            return;
        }

        if (hasFallbackDestination)
        {
            HasBegunNavigation = true;
            navAgent.SetDestination(fallbackDestination);
        }
    }

    private Transform FindSceneTransform(string objectName)
    {
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform item = allTransforms[i];
            if (item == null || item.hideFlags != HideFlags.None || !item.gameObject.scene.IsValid())
            {
                continue;
            }

            if (string.Equals(item.name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }
}
