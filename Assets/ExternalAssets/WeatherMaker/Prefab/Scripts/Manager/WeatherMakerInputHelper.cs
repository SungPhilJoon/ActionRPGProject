//
// Weather Maker for Unity
// (c) 2016 Digital Ruby, LLC
// Source code may be used for personal or commercial projects.
// Source code may NOT be redistributed or sold.
//
// *** A NOTE ABOUT PIRACY ***
//
// If you got this asset from a pirate site, please consider buying it from the Unity asset store at https://assetstore.unity.com/packages/slug/60955?aid=1011lGnL. This asset is only legally available from the Unity Asset Store.
//
// I'm a single indie dev supporting my family by spending hundreds and thousands of hours on this and other assets. It's very offensive, rude and just plain evil to steal when I (and many others) put so much hard work into the software.
//
// Thank you.
//
// *** END NOTE ABOUT PIRACY ***
//

using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace DigitalRuby.WeatherMaker
{
    /// <summary>
    /// Input helper that uses the new input system when it is enabled, otherwise the legacy input manager.
    /// </summary>
    public static class WeatherMakerInputHelper
    {
        private const float MouseAxisSensitivity = 0.1f;

        /// <summary>
        /// True if the new input system backend is enabled for the current build target.
        /// </summary>
        public static bool IsNewInputSystemEnabled
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// True if the legacy input manager backend is enabled for the current build target.
        /// </summary>
        public static bool IsLegacyInputManagerEnabled
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Get whether a key is currently pressed.
        /// </summary>
        /// <param name="key">Key code</param>
        /// <returns>True if the key is pressed, false otherwise</returns>
        public static bool GetKey(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            return GetKeyInputSystem(key, false);
#else
            return Input.GetKey(key);
#endif
        }

        /// <summary>
        /// Get whether a key was pressed this frame.
        /// </summary>
        /// <param name="key">Key code</param>
        /// <returns>True if the key was pressed this frame, false otherwise</returns>
        public static bool GetKeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            return GetKeyInputSystem(key, true);
#else
            return Input.GetKeyDown(key);
#endif
        }

        /// <summary>
        /// Get an input axis value.
        /// </summary>
        /// <param name="axisName">Axis name</param>
        /// <returns>Axis value</returns>
        public static float GetAxis(string axisName)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null || string.IsNullOrEmpty(axisName))
            {
                return 0.0f;
            }

            Vector2 delta = mouse.delta.ReadValue() * MouseAxisSensitivity;
            if (axisName == "Mouse X")
            {
                return delta.x;
            }
            else if (axisName == "Mouse Y")
            {
                return delta.y;
            }
            return 0.0f;
#else
            return Input.GetAxis(axisName);
#endif
        }

        /// <summary>
        /// Start the location service if the legacy input manager is available.
        /// </summary>
        /// <param name="desiredAccuracyInMeters">Desired accuracy in meters</param>
        /// <param name="updateDistanceInMeters">Update distance in meters</param>
        public static void StartLocationService(float desiredAccuracyInMeters, float updateDistanceInMeters)
        {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            Input.location.Start(desiredAccuracyInMeters, updateDistanceInMeters);
#endif
        }

        /// <summary>
        /// Stop the location service if the legacy input manager is available.
        /// </summary>
        public static void StopLocationService()
        {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            Input.location.Stop();
#endif
        }

        /// <summary>
        /// Try to get the current location.
        /// </summary>
        /// <param name="latitude">Latitude in degrees</param>
        /// <param name="longitude">Longitude in degrees</param>
        /// <returns>True if a current location was available, false otherwise</returns>
        public static bool TryGetLocation(out float latitude, out float longitude)
        {
            latitude = 0.0f;
            longitude = 0.0f;
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (Input.location.isEnabledByUser && Input.location.status == LocationServiceStatus.Running)
            {
                latitude = Input.location.lastData.latitude;
                longitude = Input.location.lastData.longitude;
                return true;
            }
#endif
            return false;
        }

        /// <summary>
        /// Configure an event system to use the correct input module.
        /// </summary>
        /// <param name="eventSystem">Event system</param>
        public static void ConfigureEventSystem(EventSystem eventSystem)
        {
            if (eventSystem == null)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            GameObject go = eventSystem.gameObject;
            StandaloneInputModule standaloneInputModule = go.GetComponent<StandaloneInputModule>();
            if (standaloneInputModule != null)
            {
                standaloneInputModule.enabled = false;
            }

            InputSystemUIInputModule inputSystemUIInputModule = go.GetComponent<InputSystemUIInputModule>();
            if (inputSystemUIInputModule == null)
            {
                inputSystemUIInputModule = go.AddComponent<InputSystemUIInputModule>();
            }
            inputSystemUIInputModule.enabled = true;
#endif
        }

#if ENABLE_INPUT_SYSTEM

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ConfigureLoadedEventSystems()
        {
#if UNITY_6000_3_OR_NEWER
            EventSystem[] eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
#else
            EventSystem[] eventSystems = UnityEngine.Object.FindObjectsOfType<EventSystem>();
#endif
            foreach (EventSystem eventSystem in eventSystems)
            {
                ConfigureEventSystem(eventSystem);
            }
        }

        private static bool GetKeyInputSystem(KeyCode key, bool down)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            Key inputSystemKey = ConvertKey(key);
            if (inputSystemKey == Key.None)
            {
                return false;
            }

            var control = keyboard[inputSystemKey];
            return (control != null && (down ? control.wasPressedThisFrame : control.isPressed));
        }

        private static Key ConvertKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Space: return Key.Space;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.KeypadEnter: return Key.NumpadEnter;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.BackQuote: return Key.Backquote;
                case KeyCode.Quote: return Key.Quote;
                case KeyCode.Semicolon: return Key.Semicolon;
                case KeyCode.Comma: return Key.Comma;
                case KeyCode.Period: return Key.Period;
                case KeyCode.Slash: return Key.Slash;
                case KeyCode.Backslash: return Key.Backslash;
                case KeyCode.LeftBracket: return Key.LeftBracket;
                case KeyCode.RightBracket: return Key.RightBracket;
                case KeyCode.Minus: return Key.Minus;
                case KeyCode.Equals: return Key.Equals;

                case KeyCode.A: return Key.A;
                case KeyCode.B: return Key.B;
                case KeyCode.C: return Key.C;
                case KeyCode.D: return Key.D;
                case KeyCode.E: return Key.E;
                case KeyCode.F: return Key.F;
                case KeyCode.G: return Key.G;
                case KeyCode.H: return Key.H;
                case KeyCode.I: return Key.I;
                case KeyCode.J: return Key.J;
                case KeyCode.K: return Key.K;
                case KeyCode.L: return Key.L;
                case KeyCode.M: return Key.M;
                case KeyCode.N: return Key.N;
                case KeyCode.O: return Key.O;
                case KeyCode.P: return Key.P;
                case KeyCode.Q: return Key.Q;
                case KeyCode.R: return Key.R;
                case KeyCode.S: return Key.S;
                case KeyCode.T: return Key.T;
                case KeyCode.U: return Key.U;
                case KeyCode.V: return Key.V;
                case KeyCode.W: return Key.W;
                case KeyCode.X: return Key.X;
                case KeyCode.Y: return Key.Y;
                case KeyCode.Z: return Key.Z;

                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6;
                case KeyCode.Alpha7: return Key.Digit7;
                case KeyCode.Alpha8: return Key.Digit8;
                case KeyCode.Alpha9: return Key.Digit9;

                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftCommand: return Key.LeftMeta;
                case KeyCode.RightCommand: return Key.RightMeta;
                case KeyCode.LeftWindows: return Key.LeftMeta;
                case KeyCode.RightWindows: return Key.RightMeta;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.PageDown: return Key.PageDown;
                case KeyCode.PageUp: return Key.PageUp;
                case KeyCode.Home: return Key.Home;
                case KeyCode.End: return Key.End;
                case KeyCode.Insert: return Key.Insert;
                case KeyCode.Delete: return Key.Delete;

                case KeyCode.Keypad0: return Key.Numpad0;
                case KeyCode.Keypad1: return Key.Numpad1;
                case KeyCode.Keypad2: return Key.Numpad2;
                case KeyCode.Keypad3: return Key.Numpad3;
                case KeyCode.Keypad4: return Key.Numpad4;
                case KeyCode.Keypad5: return Key.Numpad5;
                case KeyCode.Keypad6: return Key.Numpad6;
                case KeyCode.Keypad7: return Key.Numpad7;
                case KeyCode.Keypad8: return Key.Numpad8;
                case KeyCode.Keypad9: return Key.Numpad9;
                case KeyCode.KeypadDivide: return Key.NumpadDivide;
                case KeyCode.KeypadMultiply: return Key.NumpadMultiply;
                case KeyCode.KeypadMinus: return Key.NumpadMinus;
                case KeyCode.KeypadPlus: return Key.NumpadPlus;
                case KeyCode.KeypadPeriod: return Key.NumpadPeriod;
                case KeyCode.KeypadEquals: return Key.NumpadEquals;

                case KeyCode.F1: return Key.F1;
                case KeyCode.F2: return Key.F2;
                case KeyCode.F3: return Key.F3;
                case KeyCode.F4: return Key.F4;
                case KeyCode.F5: return Key.F5;
                case KeyCode.F6: return Key.F6;
                case KeyCode.F7: return Key.F7;
                case KeyCode.F8: return Key.F8;
                case KeyCode.F9: return Key.F9;
                case KeyCode.F10: return Key.F10;
                case KeyCode.F11: return Key.F11;
                case KeyCode.F12: return Key.F12;

                default: return Key.None;
            }
        }

#endif

    }
}
