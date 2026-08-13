using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles controller assignment, calibration, and registration workflow in VR registration scenarios.
/// </summary>
/// <remarks>
/// David Mertens, TH Koeln.
/// </remarks>
public class RegistrationVrController : MonoBehaviour
{
    public Registration registration;
    
    [SerializeField] private Handedness controllerSelection;
    [SerializeField] private GameObject customObject;
    
    [SerializeField] private bool calibrateObject;
    [HideInInspector] public GameObject controllerInUse;
    
    private Calibrator _calibrator;
    private Vector3 _tipPosition;
    private GameObject _demoObject;
    private bool _isRecordingCalibration;
    private bool _isRecordingTipPosition;
    private List<Vector3> _tipPositionsOverTime = new List<Vector3>();
    public readonly Vector3 PredefinedTipPosition = new Vector3(0.01211928f,-0.08250856f,-0.08393941f);
    public enum Handedness
    {
        RightHanded,
        LeftHanded
    }

    private void Awake()
    {
        _calibrator = gameObject.AddComponent<Calibrator>();
        SetupController();
        _demoObject = Helper.CreateSmallSphere();
        _demoObject.name = "Demo Object";
        _demoObject.transform.SetParent(transform);
        registration.StateChanged += OnStateChanged;
    }
    

    private void Start()
    {
        if (calibrateObject)
            registration.SetState(Registration.State.Calibration);
        else
            registration.SetState(Registration.State.MarkerSetup);
        _calibrator.SetRelativePostition(PredefinedTipPosition);
    }

    public void TriggerPressed()
    {
        if (_isRecordingCalibration || _isRecordingTipPosition) return;

        switch (registration.currentState)
        {
            case Registration.State.Calibration:
                _isRecordingCalibration = true;
                _demoObject.SetActive(true);
                _calibrator.StartRecording();
                break;
            case Registration.State.MarkerSetup:
                UpdateTipPosition();
                StartRecordingTipPosition();
                break;
        }
    }

    public void TriggerReleased()
    {
        if (_isRecordingCalibration)
        {
            _isRecordingCalibration = false;
            _calibrator.StopRecording();
        }

        if (_isRecordingTipPosition) EndRecordingTipPosition();
    }

    private void OnStateChanged()
    {
        if ((_isRecordingCalibration && registration.currentState != Registration.State.Calibration) ||
            (_isRecordingTipPosition && registration.currentState != Registration.State.MarkerSetup))
            CancelTriggerRecording();

        switch (registration.currentState)
        {
            case Registration.State.Calibration:
                _demoObject.SetActive(true);
                break;
            case Registration.State.MarkerSetup:
                _demoObject.SetActive(true);
                break;
            case Registration.State.Confirmation:
                _demoObject.SetActive(false);
                break;
        }
    }

    private void OnEnable()
    {
        SetupController();
    }

    private void OnDisable()
    {
        CancelTriggerRecording();
    }

    private void SetupController()
    {
        controllerInUse = customObject != null ? customObject : SearchForController(controllerSelection);
        _calibrator.toCalibrate = controllerInUse;
    }

    private void Update()
    {
        if (registration.currentState == Registration.State.Inactive) return;
        switch (registration.currentState)
        {
            case Registration.State.Calibration:
                CalibrationActions();
                break;
            case Registration.State.MarkerSetup:
                MarkerStateActions();
                break;
            case Registration.State.Confirmation:
                ConfirmationStateActions();
                break;
        }
    }

    private void CalibrationActions()
    {
        UpdateTipPosition();
        UpdateDemoObject();

        if (CommitButtonPressed()) registration.SetState(Registration.State.MarkerSetup);
        if (AnyTriggerDown()) TriggerPressed();
        if (AnyTriggerUp()) TriggerReleased();
        if (CancelButtonPressed())
        {
            CancelTriggerRecording();
            _calibrator.SetRelativePostition(PredefinedTipPosition);
        }
        
    }

    private void MarkerStateActions()
    {
        UpdateTipPosition();
        UpdateDemoObject();
        
        if(_isRecordingTipPosition)_tipPositionsOverTime.Add(_tipPosition);
        
        LeftHandMarkerInteractions();
        RightHandMarkerInteractions();
    }
    
    private void ConfirmationStateActions()
    {
        if (CommitButtonPressed())
        {
            registration.SaveRegistration();
            registration.SetState(Registration.State.Inactive);
        }

        if (CancelButtonPressed())
        {
            registration.SetState(Registration.State.MarkerSetup);
        }
    }

    private void UpdateTipPosition()
    {
        if (calibrateObject)
            _tipPosition = _calibrator.GetCalibratedCurrentPosition();
        else if (controllerInUse != null)
            _tipPosition = controllerInUse.transform.position + controllerInUse.transform.forward * 0.06f;
        else
            Debug.LogWarning("No Controller in Use!");
    }

    private void UpdateDemoObject()
    {
        if (_demoObject == null) return;

        _demoObject.transform.position = _tipPosition;
        Helper.SetColor(_demoObject, Helper.GetColorForIndex(registration.markers.Count));
    }

    private void RightHandMarkerInteractions()
    {
        if (AnyTriggerDown()) TriggerPressed();
        if (AnyTriggerUp()) TriggerReleased();
        if (CancelButtonPressed())
        {
            CancelTriggerRecording();
            registration.ResetEverything();
        }
    }

    private void CancelTriggerRecording()
    {
        if (_isRecordingCalibration) _calibrator.CancelRecording();

        _isRecordingCalibration = false;
        _isRecordingTipPosition = false;
        _tipPositionsOverTime.Clear();
    }

    private void StartRecordingTipPosition()
    {
        _isRecordingTipPosition = true;
        _tipPositionsOverTime.Clear();
    }
    private void EndRecordingTipPosition()
    {
        if (_tipPositionsOverTime.Count < 1)
        {
            UpdateTipPosition();
            _tipPositionsOverTime.Add(_tipPosition);
        }

        _isRecordingTipPosition = false;
        Vector3 midPoint = Vector3.zero;
        _tipPositionsOverTime.ForEach(pos => midPoint += pos);
        midPoint /= _tipPositionsOverTime.Count;
        _tipPositionsOverTime.Clear();
        registration.AddMarker(midPoint);
    }
    private GameObject SearchForController(Handedness handedness)
    {
        string controllerName =
            handedness == Handedness.RightHanded ? "RightControllerAnchor" : "LeftControllerAnchor";
        GameObject controllerToUse = GameObject.Find(controllerName);
        return controllerToUse;
    }

    private void LeftHandMarkerInteractions()
    {
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch))
        {
            registration.RestoreLastPlacedAnchor();
        }
    }

    private static bool CommitButtonPressed()
    {
        return OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch);
    }

    private static bool CancelButtonPressed()
    {
        return OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch);
    }

    private static bool AnyTriggerDown()
    {
        return OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ||
               OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
    }
    
    private static bool AnyTriggerUp()
    {
        return OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ||
               OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
    }
}
