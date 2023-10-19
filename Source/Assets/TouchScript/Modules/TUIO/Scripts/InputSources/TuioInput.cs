#if TOUCHSCRIPT_TUIO
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID

using System.Collections.Generic;
using TouchScript.Pointers;
using TouchScript.Utils;
using UnityEngine;
using UnityEngine.Assertions;
using TuioClient = MountVisual.TuioClient;

namespace TouchScript.InputSources
{
    /// <summary>
    /// Processes TUIO input received from TuioInput.
    /// </summary>
    [AddComponentMenu("TouchScript/Input Sources/TUIO Input")]
    public sealed class TuioInput : InputSource
    {
        #region Private variables
        private TuioClient.TuioInput _tuioInputClient;

        /// <summary>
        /// Map to link TuioCursors to TouchPointers. Key is TuioCusor.SessionID and Value is the TouchPointer.
        /// </summary>
        private Dictionary<uint, TouchPointer> cursorSessionIdToTouch = new Dictionary<uint, TouchPointer>(10);
        private ObjectPool<TouchPointer> touchPool;

        #endregion

        #region Constructor

        public TuioInput()
        {
            touchPool = new ObjectPool<TouchPointer>(20, () => new TouchPointer(this), null, resetPointer);
        }

        #endregion

        #region Lifecycle
        private void Awake() 
        {
            _tuioInputClient = FindObjectOfType<TuioClient.TuioInput>();
            Assert.IsNotNull(_tuioInputClient);
        }

        protected override void init()
        {
            if (_tuioInputClient != null)
            {
                _tuioInputClient.OnCursorAdded += OnCursorAdded;
                _tuioInputClient.OnCursorUpdated += OnCursorUpdated;
                _tuioInputClient.OnCursorRemoved += OnCursorRemoved;
            }
        }

        protected override void OnDisable()
        {
            if (_tuioInputClient != null)
            {
                _tuioInputClient.OnCursorAdded -= OnCursorAdded;
                _tuioInputClient.OnCursorUpdated -= OnCursorUpdated;
                _tuioInputClient.OnCursorRemoved -= OnCursorRemoved;
            }           

            base.OnDisable();
        }

        #endregion

        #region Public Methods
        public override bool UpdateInput()
        {
            if (base.UpdateInput()) return true;

            screenWidth = Screen.width;
            screenHeight = Screen.height;

            return true;
        }

        public override bool CancelPointer(Pointer pointer, bool shouldReturn) 
        {
            base.CancelPointer(pointer, shouldReturn);

            lock(this) 
            {
                if (pointer.Type == Pointer.PointerType.Touch) 
                {
                    bool foundMatch = false;
                    uint sessionId = 0;

                    foreach(var touchPoint in cursorSessionIdToTouch) 
                    {
                        if (touchPoint.Value.Id == pointer.Id) 
                        {
                            foundMatch = true;
                            sessionId = touchPoint.Key;
                            break;
                        }
                    }

                    if (foundMatch) 
                    {
                        cancelPointer(pointer);

                        if (shouldReturn)
                        {
                            cursorSessionIdToTouch[sessionId] = internalReturnTouch(pointer as TouchPointer);
                        }
                        else
                        {
                            cursorSessionIdToTouch.Remove(sessionId);
                        }
                        return true;
                    }
                }

                return false;
            }
        }

        public override void INTERNAL_DiscardPointer(Pointer pointer)
        {
            if (pointer.Type == Pointer.PointerType.Touch) {
                touchPool.Release(pointer as TouchPointer);
            }            
        }

        #endregion

        #region Private Methods
        private TouchPointer internalAddTouch(Vector2 position)
        {
            var pointer = touchPool.Get();
            pointer.Position = remapCoordinates(position);
            pointer.Buttons |= Pointer.PointerButtonState.FirstButtonDown | Pointer.PointerButtonState.FirstButtonPressed;
            addPointer(pointer);
            pressPointer(pointer);
            return pointer;
        }

        private TouchPointer internalRemoveTouch(uint id)
        {
            TouchPointer pointer;
            // Check if we have a pointer with such id
            if (!cursorSessionIdToTouch.TryGetValue(id, out pointer)) return null;

            releasePointer(pointer);
            removePointer(pointer);
            return pointer;
        }

        private TouchPointer internalReturnTouch(TouchPointer pointer)
        {
            var newPointer = touchPool.Get();
            newPointer.CopyFrom(pointer);
            pointer.Buttons |= Pointer.PointerButtonState.FirstButtonDown | Pointer.PointerButtonState.FirstButtonPressed;
            newPointer.Flags |= Pointer.FLAG_RETURNED;
            addPointer(newPointer);
            pressPointer(newPointer);
            return newPointer;
        }

        private void resetPointer(Pointer p)
        {
            p.INTERNAL_Reset();
        }

        #endregion

        #region Event Handlers
        private void OnCursorAdded(TuioClient.TuioCursor tuioCursor)
        {
            lock (this)
            {
                var x = tuioCursor.NormalizedPosition.x * screenWidth;
                var y = tuioCursor.NormalizedPosition.y * screenHeight;
                cursorSessionIdToTouch.Add(tuioCursor.SessionId, internalAddTouch(new Vector2(x, y)));
            }
        }

        private void OnCursorUpdated(TuioClient.TuioCursor tuioCursor)
        {
            lock (this) 
            {
                TouchPointer touch;
                if (!cursorSessionIdToTouch.TryGetValue(tuioCursor.SessionId, out touch)) return;

                var x = tuioCursor.NormalizedPosition.x * screenWidth;
                var y = tuioCursor.NormalizedPosition.y * screenHeight;

                touch.Position = remapCoordinates(new Vector2(x, y));
                updatePointer(touch);
            }           
        }

        private void OnCursorRemoved(TuioClient.TuioCursor tuioCursor)
        {
            lock (this)
            {
                internalRemoveTouch(tuioCursor.SessionId);
                cursorSessionIdToTouch.Remove(tuioCursor.SessionId);
            }
        }

        #endregion
    }
}

#endif
#endif
