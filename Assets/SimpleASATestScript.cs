using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class SimpleASATestScript : MonoBehaviour
{
    public GameObject _AnchorTarget;
    public TextMeshProUGUI _Debug;
    public TextMeshProUGUI _Notifcation;
    public TextMeshProUGUI _Error;
    public TextMeshProUGUI _Runs;

    float _instanitateTimer = 0;
    bool _instanated = false;   
    SpatialAnchorManager spatialAnchorManager;

    public float _Delay = 5;
    bool _ASARunning;
    float _timer = 1;
    int _runs;
    bool _first = true;
    bool _error = false;
    // Start is called before the first frame update
    
    void Start()
    {

        spatialAnchorManager = GetComponent<SpatialAnchorManager>();
        spatialAnchorManager.SessionChanged += ASA_SessionChanged;
        spatialAnchorManager.SessionDestroyed += ASA_SessionDestroy;
        spatialAnchorManager.SessionStarted += ASA_SessionStarted;
        spatialAnchorManager.SessionCreated += ASA_SessionCreated;
        spatialAnchorManager.SessionStopped += ASA_SessionStopped;
        spatialAnchorManager.SessionUpdated += ASA_SessionUpdated;
        spatialAnchorManager.LocateAnchorsCompleted += ASA_LocateAnchorsCompleted;
        spatialAnchorManager.LogDebug += ASA_LogDebug;
        spatialAnchorManager.Error += ASA_Error;

    }

    private void ASA_SessionStopped(object sender, EventArgs e)
    {
        UnityDispatcher.InvokeOnAppThread(() => Debug.LogError($"ASA_SessionStopped"));
    }

    private void ASA_SessionCreated(object sender, EventArgs e)
    {
        UnityDispatcher.InvokeOnAppThread(() => Debug.LogError($"ASA_SessionCreated"));

    }

    private void ASA_SessionStarted(object sender, EventArgs e)
    {
        UnityDispatcher.InvokeOnAppThread(() => Debug.LogError($"ASA_SessionStarted"));

    }

    private void ASA_SessionDestroy(object sender, EventArgs e)
    {
        UnityDispatcher.InvokeOnAppThread(() => Debug.LogError($"ASA_SessionDestroy"));

    }

    private void ASA_SessionChanged(object sender, EventArgs e)
    {
        UnityDispatcher.InvokeOnAppThread(() => Debug.LogError($"ASA_SessionChanged"));

    }

    private void ASA_Error(object sender, SessionErrorEventArgs args)
    {

        UnityDispatcher.InvokeOnAppThread(() =>
        {
            _Error.text = $"ASA Error: {args.ErrorMessage}";
            Debug.LogError($"ASA Error: {args.ErrorMessage}");
        });
    }

    private void ASA_LogDebug(object sender, OnLogDebugEventArgs args)
    {
        UnityDispatcher.InvokeOnAppThread(() => Debug.LogError($"ASA_LogDebug: {args.Message}"));
    }

    private void ASA_LocateAnchorsCompleted(object sender, LocateAnchorsCompletedEventArgs args)
    {
        UnityDispatcher.InvokeOnAppThread(() => Debug.LogError($"ASA_LocateAnchorsCompleted"));

    }

    private void ASA_SessionUpdated(object sender, SessionUpdatedEventArgs args)
    {
        if(!spatialAnchorManager.IsSessionStarted)
        {
            _error = true;
            UnityDispatcher.InvokeOnAppThread(() => _Error.text = "SessionUpdated while session is not started");
        }

        UnityDispatcher.InvokeOnAppThread(() => Debug.Log($"{nameof(ASA_SessionUpdated)}: {args.Status.ReadyForCreateProgress * 100f}% ready with status {args.Status.UserFeedback}"));
    }

    GameObject _instantiatedObject;
    // Update is called once per frame
    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0 && !_ASARunning && !_error)
        {
            if(_first)
            {
                _AnchorTarget.AddComponent<ARAnchor>();
                _first = false;
                _timer = 1;
            }
            else
            {
                _ASARunning = true;
                PlaceAndSendAnchor();
            }
        }

    }

    void DebugLog(string log)
    {
        Debug.Log(log);
        _Debug.text = log;
    }

    void PlaceAndSendAnchor()
    {
        DebugLog($"Host requested we place and send a world anchor");
        PlaceAndSendAnchorAsync();
    }

    async void PlaceAndSendAnchorAsync()
    {
        try
        {

            CloudSpatialAnchor placedAnchor = await AnchorPlacementTask();
            string locallyUploadedAsaId = await AnchorUploadTask(placedAnchor);

            DebugLog($"Got uploaded anchor ID: {locallyUploadedAsaId}");

            StopASASession();
            
            Notification($"Uploaded World Anchor", true);

        }
        catch (TaskCanceledException e)
        {
            DebugLog($"Placing and sending anchor cancelled: {e.Message}");
        }
        catch (System.Exception ex)
        {
            DebugLog($"Placing and sending anchor failed: {ex.Message}");
        }
        _ASARunning = false;
        _timer = _Delay;
        _runs++;

        _Runs.text = "Runs: " + _runs.ToString();
    }

    async Task<CloudSpatialAnchor> AnchorPlacementTask()
    {
        DebugLog("User placing anchor...");
        await CreateOrResetASASession();

        CloudSpatialAnchor cloudAnchor = await ConfigureWorldAnchorForUpload();
        DebugLog($"Cloud anchor configured {cloudAnchor.Identifier} IsReadyForCreate? {spatialAnchorManager.IsReadyForCreate} session status = {spatialAnchorManager.SessionStatus}");
        cloudAnchor.Expiration = System.DateTimeOffset.Now.AddDays(7);

        while (!spatialAnchorManager.IsReadyForCreate)
        {
            await Task.Delay(300);
            float createProgress = spatialAnchorManager.SessionStatus.RecommendedForCreateProgress;
            Notification($"Move your device to capture more environment data: {createProgress:0%} complete...", true);
        }

        return cloudAnchor;

    }

    async Task CreateOrResetASASession()
    {
        if (!spatialAnchorManager.IsSessionStarted)
        {
            DebugLog("Starting ASA session");
            try
            {
                await spatialAnchorManager.StartSessionAsync();
            }
            catch (Exception e)
            {
                DebugLog($"Error attempting StartSessionAsync: {e.Message}");
                Debug.LogException(e);
            }
        }
        else
        {
            foreach (var watcher in spatialAnchorManager.Session.GetActiveWatchers())
                watcher.Stop();

            DebugLog($"Resetting ASA Session");
            await spatialAnchorManager.ResetSessionAsync();
            DebugLog($"Restarted session, which now has {spatialAnchorManager.Session.GetActiveWatchers().Count} active watchers");
        }
    }

    void Notification(string text, bool persist)
    {
        _Notifcation.text = text;
    }



    private async Task<CloudSpatialAnchor> ConfigureWorldAnchorForUpload()
    {
        DebugLog($"ASA Group Manager Configuring Anchor for Upload...");
        CloudNativeAnchor nativeAnchor = _AnchorTarget.GetComponent<CloudNativeAnchor>();
        if (nativeAnchor != null)
            Destroy(nativeAnchor); // delete this one and create a fresh one
        nativeAnchor = _AnchorTarget.AddComponent<CloudNativeAnchor>();
        await nativeAnchor.NativeToCloud();
        CloudSpatialAnchor cloudAnchor = nativeAnchor.CloudAnchor;
        return cloudAnchor;
    }
    
    async Task<string> AnchorUploadTask(CloudSpatialAnchor cloudAnchor)
    {
        Notification("Uploading world anchor...", false);
        {
            await spatialAnchorManager.CreateAnchorAsync(cloudAnchor);
            if (cloudAnchor != null)
                return cloudAnchor.Identifier;
            else
                throw new System.Exception("Failed to save, but no exception was thrown.");
        }
    }

    void StopASASession()
    {
        if (spatialAnchorManager != null)
        {
            DebugLog($"Stopping and resetting ASA session with {spatialAnchorManager.Session.GetActiveWatchers().Count} current watchers");
            foreach (var watcher in spatialAnchorManager.Session.GetActiveWatchers())
                watcher.Stop();
            spatialAnchorManager.StopSession();
        }
    }



    void DownloadAndLocateAnchor(string anchorId)
    {
        Notification($"Downloading and locating world anchor", false);
        DownloadAndLocateAnchorAsync(anchorId);
    }

    SpatialAnchorLocator anchorLocator;

    async Task DownloadAndLocateAnchorAsync(string anchorId)
    {

        if (anchorLocator)
        {
            anchorLocator.Stop();
            Destroy(anchorLocator.gameObject);
        }
        await CreateOrResetASASession();

        anchorLocator = new GameObject().AddComponent<SpatialAnchorLocator>();
        var cloudAnchor = await anchorLocator.Locate(anchorId, spatialAnchorManager);

        Debug.Log($"anchorLocator.Locate({anchorId}) returned with cloudAnchor [{cloudAnchor}] (id: {cloudAnchor?.Identifier}) ");
        if (cloudAnchor != null)
        {

            // destroy the ARFoundation ARAnchor which we create on initial placement
            if (_AnchorTarget.GetComponent<ARAnchor>() is var nativeAnchor && nativeAnchor != null)
            {
                nativeAnchor.enabled = false;
                Destroy(nativeAnchor);
            }
            Pose anchorPose = cloudAnchor.GetPose();
            Debug.Log($"cloudAnchor.GetPose() with result {anchorPose.position}");
            _AnchorTarget.transform.position = anchorPose.position;
            _AnchorTarget.transform.rotation = anchorPose.rotation;

            CloudNativeAnchor cloudNativeAnchor = _AnchorTarget.GetComponent<CloudNativeAnchor>(); ;
            if(!cloudNativeAnchor)
                cloudNativeAnchor = _AnchorTarget.AddComponent<CloudNativeAnchor>();
           
            cloudNativeAnchor.CloudToNative(cloudAnchor);

            Notification($"Located world anchor successfully", true);

            Debug.Log($"Positioning local anchor complete");
        }

    }



}
