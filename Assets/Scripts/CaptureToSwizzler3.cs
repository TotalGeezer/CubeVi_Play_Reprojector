using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Ruccho.GraphicsCapture;
using Cubevi_Swizzle;

public class CaptureToSwizzler3 : MonoBehaviour
{
	[SerializeField] private Dropdown captureTargetSelect;
	[SerializeField] private SBS27Swizzler3 swizzler;

	private readonly CaptureClient client = new CaptureClient();

	private List<ICaptureTarget> listedTargets;

	private RenderTexture captureRenderTexture;

	private bool isCapturing = true;

	private IntPtr lastPtr = IntPtr.Zero;
	private int frameCounter = 0;

	void Start()
	{
		QualitySettings.vSyncCount = 1;
		Application.targetFrameRate = -1;

		UpdateTargetList();

		if (listedTargets != null && listedTargets.Count > 0)
			SetTargetIndex(listedTargets.Count - 1);
	}

	void Update()
	{
		if (!isCapturing || swizzler == null || client == null)
			return;

		Texture sourceTexture = client.GetTexture();
		if (sourceTexture == null)
			return;

		if (sourceTexture.width <= 0 || sourceTexture.height <= 0)
			return;

		IntPtr ptr = sourceTexture.GetNativeTexturePtr();
		if (ptr != lastPtr)
		{
			lastPtr = ptr;
			frameCounter++;
		}

		EnsureRenderTexture(sourceTexture.width, sourceTexture.height);

		Graphics.Blit(sourceTexture, captureRenderTexture);

		swizzler.SetSBSRenderTexture(captureRenderTexture);
	}

	private void EnsureRenderTexture(int width, int height)
	{
		if (captureRenderTexture != null &&
			captureRenderTexture.width == width &&
			captureRenderTexture.height == height)
			return;

		if (captureRenderTexture != null)
		{
			captureRenderTexture.Release();
			Destroy(captureRenderTexture);
		}

		captureRenderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
		{
			name = "CaptureRT"
		};

		captureRenderTexture.Create();

		Debug.Log($"Created CaptureRT {width}x{height}");
	}

	public void UpdateTargetList()
	{
		try
		{
			listedTargets = Utils.GetTargets()
				.Where(w => w.IsCapturable())
				.ToList();

			if (captureTargetSelect != null)
			{
				captureTargetSelect.options =
					listedTargets.Select(w => new Dropdown.OptionData(w.Description)).ToList();

				captureTargetSelect.value = 0;
				captureTargetSelect.RefreshShownValue();
			}

			Debug.Log($"Found {listedTargets.Count} capturable targets");
		}
		catch (Exception e)
		{
			Debug.LogError($"UpdateTargetList failed: {e}");
		}
	}

	public void SetTargetIndex(int index)
	{
		if (listedTargets == null || index < 0 || index >= listedTargets.Count)
			return;

		try
		{
			client.SetTarget(listedTargets[index]);
			isCapturing = true;

			Debug.Log($"Capturing: {listedTargets[index].Description}");
		}
		catch (CreateCaptureException e)
		{
			Debug.LogWarning($"Capture failed: {e.Message}");
			isCapturing = false;
		}
	}

	public void RestartCapture()
	{
		isCapturing = true;
	}

	void OnDestroy()
	{
		client?.Dispose();

		if (captureRenderTexture != null)
		{
			captureRenderTexture.Release();
			Destroy(captureRenderTexture);
			captureRenderTexture = null;
		}
	}

	void OnApplicationQuit()
	{
		client?.Dispose();
	}
}