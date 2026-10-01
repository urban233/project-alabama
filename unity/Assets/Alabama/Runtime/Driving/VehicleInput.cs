using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Alabama.Driving
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ArcadeCarController))]
    public sealed class VehicleInput : MonoBehaviour
    {
        [SerializeField] private RouteProgress route;
        private ArcadeCarController controller;
        private InputAction throttle;
        private InputAction brake;
        private InputAction steer;
        private InputAction handbrake;
        private InputAction reset;
        private InputAction pause;

        private void Awake()
        {
            controller = GetComponent<ArcadeCarController>();
            throttle = new InputAction("Throttle", InputActionType.Value);
            throttle.AddBinding("<Keyboard>/w");
            throttle.AddBinding("<Keyboard>/upArrow");
            throttle.AddBinding("<Gamepad>/rightTrigger");
            brake = new InputAction("Brake and reverse", InputActionType.Value);
            brake.AddBinding("<Keyboard>/s");
            brake.AddBinding("<Keyboard>/downArrow");
            brake.AddBinding("<Gamepad>/leftTrigger");
            steer = new InputAction("Steer", InputActionType.Value);
            steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            steer.AddBinding("<Gamepad>/leftStick/x");
            handbrake = new InputAction("Handbrake", InputActionType.Button);
            handbrake.AddBinding("<Keyboard>/space");
            handbrake.AddBinding("<Gamepad>/buttonSouth");
            reset = new InputAction("Reset", InputActionType.Button);
            reset.AddBinding("<Keyboard>/r");
            reset.AddBinding("<Gamepad>/buttonNorth");
            pause = new InputAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");
        }

        private void OnEnable()
        {
            foreach (var action in Actions()) action.Enable();
        }

        private void OnDisable()
        {
            controller.SetCommand(default);
            foreach (var action in Actions()) action.Disable();
            Time.timeScale = 1;
        }

        private void OnDestroy()
        {
            foreach (var action in Actions()) action.Dispose();
        }

        private void Update()
        {
            if (pause.WasPressedThisFrame()) Time.timeScale = Time.timeScale == 0 ? 1 : 0;
            if (reset.WasPressedThisFrame())
            {
                controller.ResetToSpawn();
                if (route != null) route.ResetProgress();
            }
            controller.SetCommand(Time.timeScale == 0 ? default :
                new VehicleCommand(throttle.ReadValue<float>(), brake.ReadValue<float>(),
                    steer.ReadValue<float>(), handbrake.IsPressed()));
        }

        private InputAction[] Actions() => new[] { throttle, brake, steer, handbrake, reset, pause };
    }
}
