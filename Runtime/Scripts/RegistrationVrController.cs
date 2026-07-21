using System.Collections.Generic;
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
    
    protected Calibrator _calibrator;
    protected Vector3 _tipPosition;
    private GameObject _demoObject;
    private bool _isRecordingTipPosition;
    private readonly List<Vector3> _tipPositionsOverTime = new List<Vector3>();
    public readonly Vector3 PredefinedTipPosition = new Vector3(0.01211928f,-0.08250856f,-0.08393941f);

    protected Handedness ControllerSelection => controllerSelection;
    protected bool CalibrateObject => calibrateObject;
    protected virtual float TipForwardOffset => 0.06f;

    public enum Handedness
    {
        RightHanded,
        LeftHanded
    }

    protected virtual void Awake()
    {
        _calibrator = gameObject.AddComponent<Calibrator>();
        SetupController();
        _demoObject = Helper.CreateSmallSphere();
        _demoObject.name = "Demo Object";
        _demoObject.transform.SetParent(transform);
        registration.StateChanged += OnStateChanged;
    }
    

    protected virtual void Start()
    {
        if (calibrateObject)
            registration.SetState(Registration.State.Calibration);
        else
            registration.SetState(Registration.State.MarkerSetup);
        _calibrator.SetRelativePostition(PredefinedTipPosition);
    }

    protected virtual void OnStateChanged()
    {
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

    protected virtual void OnEnable()
    {
        SetupController();
    }

    protected virtual void OnDisable()
    {
    }

    protected virtual void SetupController()
    {
        controllerInUse = SearchForController(controllerSelection);
        if (_calibrator != null)
            _calibrator.toCalibrate = controllerInUse;
    }

    protected virtual void Update()
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

    protected virtual void CalibrationActions()
    {
        UpdateTipPosition();
        UpdateDemoObject();

        if (CommitButtonPressed()) registration.SetState(Registration.State.MarkerSetup);
        if (AnyTriggerDown())
        {
            _demoObject.SetActive(true);
            _calibrator.StartRecording();
        }
        if (AnyTriggerUp()) _calibrator.StopRecording();
        if (CancelButtonPressed()) _calibrator.SetRelativePostition(PredefinedTipPosition);
        
    }

    protected virtual void MarkerStateActions()
    {
        UpdateTipPosition();
        UpdateDemoObject();
        
        if(_isRecordingTipPosition)_tipPositionsOverTime.Add(_tipPosition);
        
        LeftHandMarkerInteractions();
        RightHandMarkerInteractions();
    }
    
    protected virtual void ConfirmationStateActions()
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

    protected virtual void UpdateTipPosition()
    {
        if (calibrateObject)
            _tipPosition = _calibrator.GetCalibratedCurrentPosition();
        else if (controllerInUse != null)
            _tipPosition = controllerInUse.transform.position + controllerInUse.transform.forward * TipForwardOffset;
        else
            Debug.LogWarning("No Controller in Use!");
    }

    protected virtual void UpdateDemoObject()
    {
        if (_demoObject == null) return;

        _demoObject.transform.position = _tipPosition;
        Helper.SetColor(_demoObject, Helper.GetColorForIndex(registration.markers.Count));
    }

    protected virtual void RightHandMarkerInteractions()
    {
        if (_isRecordingTipPosition && AnyTriggerUp()) EndRecordingTipPosition();
        if (!_isRecordingTipPosition && AnyTriggerDown()) StartRecordingTipPosition();
        if (CancelButtonPressed()) registration.ResetEverything();
    }

    protected virtual void StartRecordingTipPosition()
    {
        _isRecordingTipPosition = true;
        _tipPositionsOverTime.Clear();
    }

    protected virtual void EndRecordingTipPosition()
    {
        if(_tipPositionsOverTime == null || _tipPositionsOverTime.Count < 1)return;
        _isRecordingTipPosition = false;
        Vector3 midPoint = Vector3.zero;
        _tipPositionsOverTime.ForEach(pos => midPoint += pos);
        midPoint /= _tipPositionsOverTime.Count;
        _tipPositionsOverTime.Clear();
        registration.AddMarker(midPoint);
    }

    protected virtual GameObject SearchForController(Handedness handedness)
    {
        string controllerName =
            handedness == Handedness.RightHanded ? "RightControllerAnchor" : "LeftControllerAnchor";
        GameObject controllerToUse = GameObject.Find(controllerName);
        return controllerToUse;
    }

    protected virtual void LeftHandMarkerInteractions()
    {
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch))
        {
            registration.RestoreLastPlacedAnchor();
        }
    }

    protected virtual bool CommitButtonPressed()
    {
        return OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch);
    }

    protected virtual bool CancelButtonPressed()
    {
        return OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch);
    }

    protected virtual bool AnyTriggerDown()
    {
        return OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ||
               OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
    }
    
    protected virtual bool AnyTriggerUp()
    {
        return OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ||
               OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
    }
}
