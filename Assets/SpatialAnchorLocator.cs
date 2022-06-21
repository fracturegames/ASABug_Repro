using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.Azure.SpatialAnchors; // for SessionUpdatedEventArgs
using Microsoft.Azure.SpatialAnchors.Unity; // For SpatialAnchorManager

// A behaviour that will look for a given anchor, provides a simplified async interface on to the SpatialAnchors SDK
public class SpatialAnchorLocator : MonoBehaviour
{
    string _targetAnchorId;
    SpatialAnchorManager _manager;
    CloudSpatialAnchorWatcher _watcher;
    CloudSpatialAnchor _result;
    bool _stopped;

    public async Task<CloudSpatialAnchor> Locate(string anchorId, SpatialAnchorManager manager)
    {
        _targetAnchorId = anchorId;
        _manager = manager;

        // setup the located callback
        _manager.AnchorLocated += ASA_AnchorLocated;

        // ensure the session is running
        if (!_manager.IsSessionStarted)
        {
            Debug.LogError($"ASA Session not started, calling SpatialAnchorManager.StartSessionAsync now");
            await _manager.StartSessionAsync();
        }

        // create the search criteria
        AnchorLocateCriteria anchorLocateCriteria = new AnchorLocateCriteria();
        anchorLocateCriteria.Identifiers = new string[] { anchorId };

        while (_result == null && !_stopped)
        {
            // create a new watcher
            Debug.Log($"Starting locate");
            _watcher = manager.Session.CreateWatcher(anchorLocateCriteria);

            while (_result == null && !_stopped)
            {
                await Task.Yield();
            }

            Debug.Log($"Anchor {_targetAnchorId} {(_result == null ? "not" : "")} located");
        }

        // return the result (or null if we were manually stopped)
        return _result;
    }

    private void ASA_AnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
        Debug.Log($"{nameof(SpatialAnchorLocator)} Anchor recognized as a possible anchor {args.Identifier} {args.Status}");

        if (args.Identifier == _targetAnchorId && args.Status == LocateAnchorStatus.Located)
            _result = args.Anchor;
    }

    private void OnDestroy()
    {
        Stop();
    }

    public void Stop()
    {
        Debug.Log($"Stop() Locator for {_targetAnchorId}");
        _stopped = true;
        if (_manager)
        {
            Debug.Log($"{nameof(SpatialAnchorLocator)} removing located callback from manager");
            _manager.AnchorLocated -= ASA_AnchorLocated;
        }
        if (_watcher != null)
        {
            Debug.Log($"{nameof(SpatialAnchorLocator)} stopping watcher");
            _watcher.Stop();
        }
    }
}
