using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Ruccho.GraphicsCapture;
using UnityEngine.UI;
using Cubevi_Swizzle;

public class CaptureToSwizzler3 : MonoBehaviour
{
	[SerializeField] private Dropdown captureTargetSelect = default;
	[SerializeField] private SBS27Swizzler3 swizzler = default;

	private CaptureClient client = new CaptureClient();
	private IEnumerable<ICaptureTarget> listedTargets;
	private RenderTexture captureRenderTexture;
	private bool isCapturing = true;

	private float captureInterval = 1f / 60f; 
	private float nextCaptureTime = 0f;

	void Start()
	{
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = 60;
		UpdateTargetList();
		if (listedTargets != null && listedTargets.Any())
		{
			SetTargetIndex(listedTargets.Count() - 1);
		}
	}

	void Update()
	{
		if (!isCapturing || swizzler == null || client == null) return;

		if (Time.unscaledTime < nextCaptureTime) return;
		nextCaptureTime = Time.unscaledTime + captureInterval;

		try
		{
			Texture sourceTexture = client.GetTexture();
			if (sourceTexture != null && sourceTexture.width > 0 && sourceTexture.height > 0)
			{
				if (captureRenderTexture == null ||
					captureRenderTexture.width != sourceTexture.width ||
					captureRenderTexture.height != sourceTexture.height)
				{
					if (captureRenderTexture != null)
						captureRenderTexture.Release();

					captureRenderTexture = new RenderTexture(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32);
					captureRenderTexture.Create();

					Debug.Log($"Created capture render texture: {sourceTexture.width}x{sourceTexture.height}");
				}

				Graphics.Blit(sourceTexture, captureRenderTexture);
				swizzler.SetSBSRenderTexture(captureRenderTexture);
			}
		}
		catch (Exception e)
		{
			Debug.LogWarning($"Capture error: {e.Message}");
			isCapturing = false;
		}
	}

	public void UpdateTargetList()
	{
		try
		{
			var windows = Utils.GetTargets().Where(w => w.IsCapturable());
			listedTargets = windows.ToList();

			if (captureTargetSelect != null)
			{
				captureTargetSelect.options = listedTargets.Select(w => new Dropdown.OptionData(w.Description)).ToList();
				if (captureTargetSelect.options.Count > 0)
					captureTargetSelect.value = 0;
				captureTargetSelect.RefreshShownValue();
			}

			Debug.Log($"Found {listedTargets.Count()} capturable targets");
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to update target list: {e.Message}");
		}
	}

	public void SetTargetIndex(int index)
	{
		if (listedTargets == null || index >= listedTargets.Count()) return;

		try
		{
			client.SetTarget(listedTargets.ElementAt(index));
			isCapturing = true;
			Debug.Log($"Capturing: {listedTargets.ElementAt(index).Description}");
		}
		catch (CreateCaptureException e)
		{
			Debug.LogWarning($"Cannot capture this target: {e.Message}");
			isCapturing = false;
		}
	}

	public void RestartCapture()
	{
		isCapturing = true;
	}

	private void OnDestroy()
	{
		client?.Dispose();
		if (captureRenderTexture != null)
		{
			captureRenderTexture.Release();
			Destroy(captureRenderTexture);
		}
	}

	private void OnApplicationQuit()
	{
		client?.Dispose();
	}
}