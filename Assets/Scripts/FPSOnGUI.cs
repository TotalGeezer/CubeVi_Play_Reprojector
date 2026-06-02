using UnityEngine;

public class FPSOnGUI : MonoBehaviour
{
	private float deltaTime;
	private float fps;

	private GUIStyle style;
	private Rect rect;

	void Awake()
	{
		// Optional framerate settings
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = 60;

		style = new GUIStyle();
		style.normal.textColor = Color.white;
		style.padding = new RectOffset(10, 10, 5, 5);
		style.normal.background = Texture2D.blackTexture;

		// Original sizing
		rect = new Rect(10, 10, 400, 120);
	}

	void Update()
	{
		// Smoothed FPS calculation
		deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
		fps = 1.0f / deltaTime;
	}

	void OnGUI()
	{
		// Original dynamic scaling
		style.fontSize = Mathf.RoundToInt(Screen.height * 0.035f);

		GUI.Label(rect,
			$"FPS: {Mathf.Ceil(fps)}",
			style);
	}
}