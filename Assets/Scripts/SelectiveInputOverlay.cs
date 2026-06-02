using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectiveInputOverlay : MonoBehaviour
{
	[DllImport("user32.dll")]
	private static extern IntPtr GetActiveWindow();

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

	[DllImport("user32.dll")]
	private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(
		IntPtr hWnd,
		IntPtr hWndInsertAfter,
		int X,
		int Y,
		int cx,
		int cy,
		uint uFlags
	);

	[DllImport("user32.dll")]
	private static extern bool SetLayeredWindowAttributes(
		IntPtr hwnd,
		uint crKey,
		byte bAlpha,
		uint dwFlags
	);

	private const int GWL_EXSTYLE = -20;

	private const uint WS_EX_LAYERED = 0x80000;
	private const uint WS_EX_TRANSPARENT = 0x20;
	private const uint WS_EX_TOPMOST = 0x8;
	private const uint WS_EX_NOACTIVATE = 0x08000000;

	private const uint LWA_ALPHA = 0x2;

	private const uint SWP_NOMOVE = 0x0002;
	private const uint SWP_NOSIZE = 0x0001;
	private const uint SWP_NOACTIVATE = 0x0010;

	private IntPtr windowHandle;
	private bool clickThroughEnabled = true;
	private bool overlayModeActive = false;
	private bool hideCursor = false;

	public bool IsOverlayModeActive => overlayModeActive;
	public bool IsClickThroughEnabled => clickThroughEnabled;

	[Header("Settings")]
	[SerializeField] private bool startOverlayOnAwake = false;
	[SerializeField] private byte overlayAlpha = 255; // 0-255
	[SerializeField] private bool autoUpdateClickThrough = true;

	private void Awake()
	{
		if (startOverlayOnAwake)
		{
			StartOverlayMode();
		}
	}

	private void Update()
	{
#if !UNITY_EDITOR
        if (overlayModeActive && autoUpdateClickThrough)
        {
            bool overUI = IsPointerOverUI();
            // If over UI -> disable click-through
            // If not over UI -> enable click-through
            SetClickThrough(!overUI);
        }
#endif
	}

	private void OnDestroy()
	{
		if (overlayModeActive)
		{
			StopOverlayMode();
		}
	}

	private void OnApplicationQuit()
	{
		if (overlayModeActive)
		{
			StopOverlayMode();
		}
	}

	/// <summary>
	/// Toggles overlay mode on/off
	/// </summary>
	public void ToggleOverlayMode()
	{
		if (!overlayModeActive)
		{
			StartOverlayMode();
		}
		else
		{
			StopOverlayMode();
		}
	}

	/// <summary>
	/// Starts overlay mode with current settings
	/// </summary>
	public void StartOverlayMode()
	{
#if !UNITY_EDITOR
        if (overlayModeActive) return;

        windowHandle = GetActiveWindow();
        if (windowHandle == IntPtr.Zero)
        {
            Debug.LogError("Failed to get active window handle");
            return;
        }

        SetupOverlayBasic();
        SetClickThrough(true);
        overlayModeActive = true;
        
        Debug.Log("Overlay mode started");
#else
		Debug.LogWarning("SelectiveInputOverlay only works in standalone builds, not in Editor");
#endif
	}

	/// <summary>
	/// Stops overlay mode and reverts window to normal behavior
	/// </summary>
	public void StopOverlayMode()
	{
#if !UNITY_EDITOR
        if (!overlayModeActive || windowHandle == IntPtr.Zero) return;

        // Remove all extended window styles
        uint exStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
        exStyle &= ~(WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT);
        SetWindowLong(windowHandle, GWL_EXSTYLE, exStyle);

        // Reset window positioning to normal
        SetWindowPos(
            windowHandle,
            IntPtr.Zero,  // Not topmost
            0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE
        );

        overlayModeActive = false;
        clickThroughEnabled = false;
        
        // Restore cursor if it was hidden
        if (hideCursor)
        {
            ToggleHideCursor(); // This will restore cursor visibility
        }
        
        Debug.Log("Overlay mode stopped");
#endif
	}

	private void SetupOverlayBasic()
	{
		uint exStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);

		// Add layered, topmost, and no-activate styles
		exStyle |= WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_NOACTIVATE;

		SetWindowLong(windowHandle, GWL_EXSTYLE, exStyle);

		// Set as topmost window
		SetWindowPos(
			windowHandle,
			new IntPtr(-1), // HWND_TOPMOST
			0, 0, 0, 0,
			SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE
		);

		// Set transparency (fully opaque by default)
		SetLayeredWindowAttributes(windowHandle, 0, overlayAlpha, LWA_ALPHA);
	}

	private bool IsPointerOverUI()
	{
		// Unity UI check
		if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
		{
			return true;
		}
		return false;
	}

	private void SetClickThrough(bool enable)
	{
		if (clickThroughEnabled == enable)
			return;

		if (windowHandle == IntPtr.Zero)
			return;

		clickThroughEnabled = enable;

		uint exStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);

		if (enable)
		{
			exStyle |= WS_EX_TRANSPARENT;
		}
		else
		{
			exStyle &= ~WS_EX_TRANSPARENT;
		}

		SetWindowLong(windowHandle, GWL_EXSTYLE, exStyle);
	}

	/// <summary>
	/// Toggles cursor visibility
	/// </summary>
	public void ToggleHideCursor()
	{
		hideCursor = !hideCursor;
		Cursor.visible = !hideCursor;
	}

	/// <summary>
	/// Sets cursor visibility explicitly
	/// </summary>
	public void SetCursorVisible(bool visible)
	{
		hideCursor = !visible;
		Cursor.visible = visible;
	}

	/// <summary>
	/// Manually sets whether clicks should pass through or be blocked
	/// </summary>
	public void ForceClickThrough(bool enable)
	{
		if (!overlayModeActive)
		{
			Debug.LogWarning("Overlay mode is not active. Start overlay mode first.");
			return;
		}

		SetClickThrough(enable);
	}

	/// <summary>
	/// Updates the overlay transparency
	/// </summary>
	public void SetOverlayAlpha(byte alpha)
	{
		overlayAlpha = alpha;
		if (overlayModeActive && windowHandle != IntPtr.Zero)
		{
			SetLayeredWindowAttributes(windowHandle, 0, overlayAlpha, LWA_ALPHA);
		}
	}

	/// <summary>
	/// Enables/disables automatic click-through based on UI interaction
	/// </summary>
	public void SetAutoUpdateClickThrough(bool enabled)
	{
		autoUpdateClickThrough = enabled;
	}
}