﻿using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.SceneManagement;

namespace Skibitsky.InputSystem
{
    public class InputHandler : MonoBehaviour, IInputHandler
    {

        #region Interface implementation

        public string Name { get { return _name; } }
        public bool InvokeOncePerFrame { get { return _invokeOncePerFrame; } }
        public CursorLockMode CursorLockMode { get { return _cursorLockMode; } }

        public bool HardBlockKeys { get { return _hardBlockKeys; } }
        public bool BlockKeys { get { return _blockKeys; } }

        public bool HardBlockAxes { get { return _hardBlockAxes; } }
        public List<InputAxis> Axes { get { return _axes; } }

        public Dictionary<KeyCode, InputListener> JustPressed { get { return _justPressed; } }
        public Dictionary<KeyCode, InputListener> Pressed { get { return _pressed; } }
        public Dictionary<KeyCode, InputListener> JustReleased { get { return _justReleased; } }

        public bool isDirty { get; set; }

        #endregion

        [Header("Main Handler Settings")]
        // Name of input handler represents its purpose and can be shown in settings for example
        // It's also used in AllInputHandlers dic in InptuManager
        [SerializeField]
        private string _name = "Player Movement";

        // If true - each listener action will be invoked only once per frame.
        // So if both Positive and Alternative keys are pressed at one time, 
        // Action won't be invoked twice.
        [Tooltip("If true - each listener action will be invoked only once per frame")]
        [SerializeField]
        private bool _invokeOncePerFrame = true;

        [Tooltip("Should handler be added to the Stack after level was loaded?")]
        [SerializeField]
        private bool _addToStackOnLoad = false;

        // InputManager sets up this value to the Cursor.lockState if the handler
        // is on the top of the Stack
        [Tooltip("Cursor lock state if the Handler will be on the top of the Stack")]
        [SerializeField]
        private CursorLockMode _cursorLockMode = CursorLockMode.Confined;

        // To avoid double init.
        private bool _inited = false;

        #region Keys settings
        [Header("Keys Settings")]
        // If true it InputManager will work only with this handler's keys f it is on the top of stack
        [Tooltip("If true InputManager will work only with this handler's keys f it is on the top of stack")]
        [SerializeField]
        private bool _hardBlockKeys = false;

        // Should InputManager stop on this handler if it contains called key?
        [Tooltip("Should InputManager stop on this handler if it contains called key?")]
        [SerializeField]
        private bool _blockKeys = true;

        // Lists are used to fill default Handler values tight from Unity Editor
        [Tooltip("GetKeyDown")]
        [SerializeField]
        List<InputListener> JustPressedTemplate = new List<InputListener>();
        [Tooltip("GetKey")]
        [SerializeField]
        List<InputListener> PressedTemplate = new List<InputListener>();
        [Tooltip("GetKeyUp")]
        [SerializeField]
        List<InputListener> JustReleasedTemplate = new List<InputListener>();
        #endregion

        #region Axes settings
        [Header("Axes Settings")]
        // Should InputManager block all axes in handlers which comes after him in the Stack?
        [SerializeField]
        private bool _hardBlockAxes = false;

        // All Handler's axes
        [SerializeField]
        private List<InputAxis> _axes = new List<InputAxis>();
        #endregion

        #region Keys Dictionaries
        // JustPressed (Input.GetKeyDown)
        private Dictionary<KeyCode, InputListener> _justPressed = new Dictionary<KeyCode, InputListener>();
        // Pressed (Input.GetKey)
        private Dictionary<KeyCode, InputListener> _pressed = new Dictionary<KeyCode, InputListener>();
        // JustReleased (Input.GetKeyUp)
        private Dictionary<KeyCode, InputListener> _justReleased = new Dictionary<KeyCode, InputListener>();
        #endregion

        // All Listeners from all dictionaries
        Dictionary<string, InputListener> AllListeners = new Dictionary<string, InputListener>();

        public InputHandler()
        {
            isDirty = false;
        }

        // Asks InputManager to init this handler 
        // in case GameObject was created after InputManager Awake.
        private void OnEnable()
        {
            InputManager.instance.InitNewInputHandler(this);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Because each scene has unique InputManager and we have to init
        // InputHandler each time
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InputManager.instance.InitNewInputHandler(this);
            if (_addToStackOnLoad)
                InputManager.instance.AddInputHandlerToStack(this);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// Fills dictionaries with InputListeners from the saving file.
        /// If file doesn't exist - with default data filled from the Editor.
        /// <remark> Called from InputManager Awake() </remark>
        /// </summary>
        public bool Init()
        {
            if (_inited) return false;

            List<InputListener> JustPressedSource;
            List<InputListener> PressedSource;
            List<InputListener> JustReleasedSource;

            var savedHandler = InputSaver.ReadHandler(this.Name);

            if (savedHandler != null)
            {
                JustPressedSource = savedHandler.JustPressed;
                PressedSource = savedHandler.Pressed;
                JustReleasedSource = savedHandler.JustReleased;
                _axes = savedHandler.Axes;
            }
            else
            {
                JustPressedSource = JustPressedTemplate;
                PressedSource = PressedTemplate;
                JustReleasedSource = JustReleasedTemplate;
            }

            foreach (var l in JustPressedSource)
            {
                if (l.Positive != KeyCode.None) JustPressed.Add(l.Positive, l);
                if (l.Alternative != KeyCode.None) JustPressed.Add(l.Alternative, l);

                if (!AllListeners.ContainsKey(l.Name)) AllListeners.Add(l.Name, l);
            }

            foreach (var l in PressedSource)
            {
                if (l.Positive != KeyCode.None) Pressed.Add(l.Positive, l);
                if (l.Alternative != KeyCode.None) Pressed.Add(l.Alternative, l);

                if (!AllListeners.ContainsKey(l.Name)) AllListeners.Add(l.Name, l);
            }

            foreach (var l in JustReleasedSource)
            {
                if (l.Positive != KeyCode.None) JustReleased.Add(l.Positive, l);
                if (l.Alternative != KeyCode.None) JustReleased.Add(l.Alternative, l);

                if (!AllListeners.ContainsKey(l.Name)) AllListeners.Add(l.Name, l);
            }

            _inited = true;

            if (savedHandler == null) SaveHandler();

            return true;
        }

        private void LateUpdate()
        {
            #if UNITY_STANDALONE
            // If InvokeOncePerFrame, at the end of frame we have to set InputListener.Invoked to false
            // That it could be invoked in the next frame
            if (!InvokeOncePerFrame) return;
            foreach (var il in JustPressed)
                il.Value.Invoked = false;

            foreach (var il in Pressed)
                il.Value.Invoked = false;

            foreach (var il in JustReleased)
                il.Value.Invoked = false;
            #endif
        }

        /// <summary>
        /// Returns all KeyCodes used in all handler's dictionaries.
        /// </summary>
        /// <remarks> 
        /// Used in InputManager to update KeyCodesToListen list in 
        /// InputManager.UpdateKeyCodes()
        /// </remarks>
        public List<KeyCode> GetAllKeyCodes()
        {
            return new List<KeyCode>().Concat(JustPressed.Keys)
                .Concat(Pressed.Keys)
                .Concat(JustReleased.Keys)
                .ToList();
        }

        /// <summary>
        /// Returns InputListener from the handler by name. 
        /// It can return null if there is no listener with the name.
        /// </summary>
        /// <param name="name">Name of InputListener</param>
        /// <returns></returns>
        public InputListener GetListener(string name)
        {
            InputListener il;
            AllListeners.TryGetValue(name, out il);
            return il;
        }

        /// <summary>
        /// Changes key in runtime to access listener in all dictionaries. 
        /// </summary>
        /// <param name="listener">Input listener to be changed</param>
        /// <param name="from">Old key</param>
        /// <param name="to">New key</param>
        public void ChangeKey(string listener, KeyCode from, KeyCode to)
        {
            InputListener temp;
            Pressed.TryGetValue(from, out temp);
            if (temp != null && temp.Name == listener && !Pressed.ContainsKey(to))
            {
                Pressed.Remove(from);
                Pressed.Add(to, temp);
            }

            JustPressed.TryGetValue(from, out temp);
            if (temp != null && temp.Name == listener && !JustPressed.ContainsKey(to))
            {
                JustPressed.Remove(from);
                JustPressed.Add(to, temp);
            }

            JustReleased.TryGetValue(from, out temp);
            if (temp != null && temp.Name == listener && !JustReleased.ContainsKey(to))
            {
                JustReleased.Remove(from);
                JustReleased.Add(to, temp);
            }

            SaveHandler();
        }

        /// <summary>
        /// Save setting to the file.
        /// </summary>
        public void SaveHandler()
        {
            InputSaver.WriteHandler(this);
        }

        private void OnDestroy()
        {
            InputManager.instance.DeleteHandler(this);
        }
        

        /// <summary>
        /// Adds action to the InputListiner of specific name in 
        /// </summary>
        /// <param name="name">Name of InputListener</param>
        /// <param name="method">Method to be added to the listener</param>
        private void AddAction(string name, Action method, IDictionary<KeyCode, InputListener> dic)
        {
            foreach (var inputListener in dic.Values)
            {
                if(inputListener.Name != name) continue;
                inputListener.Actions += method;
                isDirty = true;
                break;
            }
        }
        
        /// <summary>
        /// Adds action to the InputListiner of specific name in 
        /// </summary>
        /// <param name="name">Name of InputListener</param>
        /// <param name="method">Method to be added to the listener</param>
        private void AddAction(KeyCode key, Action method, IDictionary<KeyCode, InputListener> dic)
        {
            if (dic.ContainsKey(key))
                dic[key].Actions += method;
            else
            {
                var il = new InputListener(key);
                il.Actions += method;
                dic.Add(key, il);
                isDirty = true;
            }
        }
        
        /// <summary>
        /// Removes action from InputListener based on the name of listener
        /// </summary>
        /// <param name="name">Name of the listener</param>
        /// <param name="method">Method to be removed</param>
        public void RemoveAction(string name, Action method, IDictionary<KeyCode, InputListener> dic)
        {
            foreach (var l in dic.Values)
            {
                if (l.Name != name) continue;
                l.Actions -= method;
                if (l.Actions.GetInvocationList().Length != 0) continue;

                if (dic.ContainsKey(l.Positive))
                    dic.Remove(l.Positive);
                if (dic.ContainsKey(l.Alternative))
                    dic.Remove(l.Alternative);
                isDirty = true;
            }
        }
        
        /// <summary>
        /// Removes action from InputListener based on the KeyCode.
        /// </summary>
        /// <param name="key">KeyCode of the key the method was subscribed to</param>
        /// <param name="method">Method to be removed</param>
        public void RemoveAction(KeyCode key, Action method, IDictionary<KeyCode, InputListener> dic)
        {
            if (!dic.ContainsKey(key)) return;
            dic[key].Actions -= method;
            if (dic[key].Actions.GetInvocationList().Length != 0) return;
            dic.Remove(key);
            isDirty = true;
        }

        
        // -------------------------------------------------
        
        
        #region JustPressed
        /// <summary>
        /// Adds passed action to the InputListiner of specific name in 
        /// JustPressed dictionary.
        /// </summary>
        /// <param name="name">Name of InputListener</param>
        /// <param name="method">Method to be added to the listener</param>
        public void AddJustPressedAction(string name, Action method)
        {  
            AddAction(name, method, JustPressed);
        }

        /// <summary>
        /// Adds passed action to the InputListener of specific key in 
        /// JustPressed dictionary.
        /// </summary>
        /// <param name="key">KeyCode of the key the method to be subscribed to</param>
        /// <param name="method">Method to be subscribed</param>
        public void AddJustPressedAction(KeyCode key, Action method)
        {
            AddAction(key, method, JustPressed); 
        }

        /// <summary>
        /// Removes passed action from InputListener based on the name of listener
        /// </summary>
        /// <param name="name">Name of the listener</param>
        /// <param name="method">Method to be removed</param>
        public void RemoveJustPressedAction(string name, Action method)
        {
            RemoveAction(name, method, JustPressed);
        }
    

        /// <summary>
        /// Removes passed action from InputListener based on the KeyCode.
        /// </summary>
        /// <param name="key">KeyCode of the key the method was subscribed to</param>
        /// <param name="method">Method to be removed</param>
        public void RemoveJustPressedAction(KeyCode key, Action method)
        {
            RemoveAction(key, method, JustPressed);
        }
        #endregion

        #region Pressed
        /// <summary>
        /// Adds passed action to the InputListener of specific name in 
        /// Pressed dictionary.
        /// </summary>
        /// <param name="name">Name of InputListener</param>
        /// <param name="method">Method to be added to the listener</param>
        public void AddPressedAction(string name, Action method)
        {
            AddAction(name, method, Pressed);
        }

        /// <summary>
        /// Adds passed action to the InputListener of specific key in 
        /// Pressed dictionary.
        /// </summary>
        /// <param name="key">KeyCode of the key the method to be subscribed to</param>
        /// <param name="method">Method to be subscribed</param>
        public void AddPressedAction(KeyCode key, Action method)
        {
            AddAction(key, method, Pressed);
        }

        /// <summary>
        /// Removes passed action from InputListener based on the name of listener
        /// </summary>
        /// <param name="name">Name of the listener</param>
        /// <param name="method">Method to be removed</param>
        public void RemovePressedAction(string name, Action method)
        {
            RemoveAction(name, method, Pressed);
        }

        /// <summary>
        /// Removes passed action from InputListener based on the KeyCode.
        /// </summary>
        /// <param name="key">KeyCode of the key the method was subscribed to</param>
        /// <param name="method">Method to be removed</param>
        public void RemovePressedAction(KeyCode key, Action method)
        {
            RemoveAction(key, method, Pressed);
        }
        #endregion

        #region JustReleased
        /// <summary>
        /// Adds passed action to the InputListener of specific name in 
        /// JustReleased dictionary.
        /// </summary>
        /// <param name="name">Name of InputListener</param>
        /// <param name="method">Method to be added to the listener</param>
        public void AddJustReleasedAction(string name, Action method)
        {
            AddAction(name, method, JustReleased);
        }

        /// <summary>
        /// Adds passed action to the InputListiner of specific key in 
        /// JustReleased dictionary.
        /// </summary>
        /// <param name="key">KeyCode of the key the method to be subscribed to</param>
        /// <param name="method">Method to be subscribed</param>
        public void AddJustReleasedAction(KeyCode key, Action method)
        {
            AddAction(key, method, JustReleased);
        }

        /// <summary>
        /// Removes passed action from InputListener based on the name of listener
        /// </summary>
        /// <param name="name">Name of the listener</param>
        /// <param name="method">Method to be removed</param>
        public void RemoveJustReleasedAction(string name, Action method)
        {
            RemoveAction(name, method, JustReleased);
        }

        /// <summary>
        /// Removes passed action from InputListener based on the KeyCode.
        /// </summary>
        /// <param name="key">KeyCode of the key the method was subscribed to</param>
        /// <param name="method">Method to be removed</param>
        public void RemoveJustReleasedAction(KeyCode key, Action method)
        {
            RemoveAction(key, method, JustReleased);
        }

        #endregion
    }
}