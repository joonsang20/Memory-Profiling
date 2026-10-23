using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class NativeMemoryDemo : MonoBehaviour
{
    [SerializeField] int sizeMB    = 100;
    [SerializeField] int pageStride = 4096;

    enum DemoState { None, Allocated, Touched }

    IntPtr    _ptr   = IntPtr.Zero;
    long      _sizeBytes;
    DemoState _state = DemoState.None;

    bool     _uiVisible = true;
    string   _cachedUI  = "";
    GUIStyle _uiStyle;

    void Start()
    {
        BuildUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha4)) Allocate();
        if (Input.GetKeyDown(KeyCode.Alpha5)) TouchPages();
        if (Input.GetKeyDown(KeyCode.Alpha6)) Free();
        if (Input.GetKeyDown(KeyCode.F1))     _uiVisible = !_uiVisible;
    }

    void OnGUI()
    {
        if (!_uiVisible) return;

        if (_uiStyle == null)
        {
            _uiStyle          = new GUIStyle(GUI.skin.label);
            _uiStyle.fontSize = 22;
            _uiStyle.normal.textColor = Color.white;
        }

        GUI.Label(new Rect(Screen.width - 320, 10, 310, 160), _cachedUI, _uiStyle);
    }

    void Allocate()
    {
        if (_state != DemoState.None) return;

        _sizeBytes = (long)sizeMB * 1024 * 1024;
        _ptr       = Marshal.AllocHGlobal((int)_sizeBytes);
        _state     = DemoState.Allocated;
        BuildUI();
    }

    void TouchPages()
    {
        if (_state != DemoState.Allocated) return;

        for (long offset = 0; offset < _sizeBytes; offset += pageStride)
            Marshal.WriteByte(_ptr + (int)offset, 1);

        _state = DemoState.Touched;
        BuildUI();
    }

    void Free()
    {
        if (_state == DemoState.None) return;

        Marshal.FreeHGlobal(_ptr);
        _ptr   = IntPtr.Zero;
        _state = DemoState.None;
        BuildUI();
    }

    void OnDestroy()
    {
        if (_ptr != IntPtr.Zero) Free();
    }

    void OnApplicationQuit()
    {
        if (_ptr != IntPtr.Zero) Free();
    }

    void BuildUI()
    {
        _cachedUI = string.Format(
            "Native Memory\nState : {0}\nSize  : {1} MB\n\n[4] Alloc  [5] Touch  [6] Free",
            _state,
            sizeMB
        );
    }
}
