using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Cubevi_Swizzle;
public class SBS27Swizzler3 : MonoBehaviour
{
	#region Grid Camera Config
	[Header("Grid Camera Related Settings")]
	public Transform RootTransfrom;
	public GameObject BatchCameraPrefab;
	public bool useCameraPrefab = false;

	public GameObject uiOverlayParent;

	public Camera[] _batchcameras;
	public RenderTexture[] cameraRenderTextures;
	private Texture2DArray textureArray;
	#endregion

	#region Device / SBS Input
	public DeviceData _device;

	private RenderTexture _sbsSource;

	public Material _splitMat;
	public Material _batchMaterial;
	private Camera displayCamera;
	#endregion

	#region Focal Config
	[Header("Focal Settings")]
	public Transform TargetTransform;
	public float FocalPlane = 10f;
	public bool useTartgetFocal = true;

	private Transform Root;
	private Transform Target;
	#endregion

	#region Interlace Params
	public float interval_fov = 29.5f;
	public float delta_pos = 0.065f;
	public float cam_fov_x = 60f;

	public float rowNum = 2f;
	public float colNum = 2f;
	#endregion

	#region 27-inch Specific X0 Array
	[Header("27-inch X0 Array")]
	public float[] X0Array = new float[77]; // Critical for 27-inch display calibration
	#endregion

	#region Eye Tracking
	public EyeTrackingManager _eye;
	public bool useEyeTrackingXOffset = false;
	[Range(0.2f, 1.8f)]
	public float eyeTrackingK = 1.0f;

	private bool useEyeTracking = true;
	private Vector2 eyeOffset;
	#endregion

	#region Device Configuration Fields
	private float x0 = 18.0f;
	private float interval = 7.53800f;
	private float slope = -0.133632f;
	private float delta_700_pos = 0.0715f;
	private float mycam_tan;
	private int targetScreenIndex = 0;
	#endregion

	private bool isSBS = true;

	#region Unity Init
	private void Awake()
	{
		DontDestroyOnLoad(gameObject);
		InitializeDeviceData();
		_device.x0 = 5.4f;
		_device.interval = 7.702f;
		_device.slope = -0.2516f;
	}

	private void Start()
	{
		Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
		ConfigureViewParameters();
		InitRenderTextures();
		LoadMaterials();

		InitTarget();
		InitCamera();
		InitializeDisplayCamera();
		StartEyeTracking();

		if (_eye != null)
			_eye.OnX0ArrayAvailable += HandleX0ArrayUpdate;
	}

	private void OnDestroy()
	{
		if (_eye != null)
			_eye.OnX0ArrayAvailable -= HandleX0ArrayUpdate;

		if (_splitMat != null)
			Destroy(_splitMat);
	}
	#endregion

	#region Display Initialization
	private void InitializeDisplayCamera()
	{
		GameObject displayCameraObj = new GameObject("_DisplayCamera");
		DontDestroyOnLoad(displayCameraObj);

		displayCamera = displayCameraObj.AddComponent<Camera>();
		displayCamera.targetDisplay = targetScreenIndex;
		displayCamera.enabled = true;
		displayCamera.depth = 100;
		displayCamera.cullingMask = ~0;

		GameObject canvasObj = new GameObject("DisplayCanvas");
		canvasObj.transform.SetParent(displayCamera.transform);
		Canvas canvas = canvasObj.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceCamera;
		canvas.worldCamera = displayCamera;

		canvasObj.AddComponent<GraphicRaycaster>();

		CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(_device.output_size_X, _device.output_size_Y);

		GameObject imageObj = new GameObject("Batch");
		imageObj.transform.SetParent(canvasObj.transform, false);
		RawImage batchImage = imageObj.AddComponent<RawImage>();
		batchImage.material = _batchMaterial;
		batchImage.raycastTarget = false;
		batchImage.maskable = false;

		imageObj.GetComponent<RectTransform>().sizeDelta = new Vector2(_device.output_size_X, _device.output_size_Y);

		if (uiOverlayParent != null)
		{
			uiOverlayParent.transform.SetParent(canvasObj.transform, false);
		}
	}

	private void StartEyeTracking()
	{
		if (_eye != null)
		{
			_eye.StartTracking();
			useEyeTracking = true;
		}
	}

	private void HandleX0ArrayUpdate(float[] data)
	{
		if (data != null && X0Array != null && data.Length == X0Array.Length)
		{
			Array.Copy(data, X0Array, data.Length);

			if (_batchMaterial != null)
			{
				_batchMaterial.SetFloatArray("_X0Array", X0Array);
			}
		}
	}
	#endregion

	#region Input
	public void SetSBSRenderTexture(RenderTexture source)
	{
		_sbsSource = source;
	}
	#endregion

	#region Init
	private void InitializeDeviceData()
	{
		LoadConfigFromJson();

		_device = new DeviceData
		{
			total_viewnum = 2,
			imgs_count_x = 1,
			imgs_count_y = 2,
			total_fov = 29.5f,
			sbs_fov = 29.5f,
			subimg_width = 2560,
			subimg_height = 1440,
			output_size_X = 5120f,
			output_size_Y = 2880f,
			cam_fov_x = cam_fov_x,
			interval_fov = interval_fov,
			delta_pos = delta_pos,
			delta_700_pos = delta_700_pos,
			rowNum = rowNum,
			colNum = colNum,
			x0 = x0,
			interval = interval,
			slope = slope,
			is27Layout = true,
			f_cam = 2778.0f,
			tan_alpha_2 = 0.3456f,
			pixel_order = "rgb_012"
		};

		mycam_tan = Mathf.Tan((_device.cam_fov_x / 2) * Mathf.Deg2Rad);
	}

	private void LoadConfigFromJson()
	{
		string jsonPath = Path.Combine(Application.streamingAssetsPath, "eye_tracking_x0.json");
		if (!File.Exists(jsonPath))
		{
			Debug.LogWarning("eye_tracking_x0.json not found, using default 27-inch parameters");
			return;
		}

		try
		{
			string jsonText = File.ReadAllText(jsonPath);
			JObject root = JObject.Parse(jsonText);

			if (root["grating_params"] is JObject gratingParams)
			{
				if (gratingParams["x0"] != null)
					x0 = (float)gratingParams["x0"];
				if (gratingParams["interval"] != null)
					interval = (float)gratingParams["interval"];
				if (gratingParams["slope"] != null)
					slope = (float)gratingParams["slope"];
			}

			if (root["cam_para"] is JObject camParams)
			{
				if (camParams["delta_x_700"] != null)
					delta_700_pos = (float)camParams["delta_x_700"];
			}

			Debug.Log($"Loaded 27-inch config: x0={x0}, interval={interval}, slope={slope}");
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to read eye_tracking_x0.json: {e.Message}");
		}
	}

	private void ConfigureViewParameters()
	{
		_device.cam_fov_x = cam_fov_x;
		_device.interval_fov = interval_fov;
		_device.delta_pos = delta_pos;

		isSBS = true;
	}

	private void InitRenderTextures()
	{
		cameraRenderTextures = new RenderTexture[2];

		cameraRenderTextures[0] = new RenderTexture((int)_device.subimg_width, (int)_device.subimg_height, 24);
		cameraRenderTextures[1] = new RenderTexture((int)_device.subimg_width, (int)_device.subimg_height, 24);

		cameraRenderTextures[0].Create();
		cameraRenderTextures[1].Create();

		textureArray = new Texture2DArray(
			(int)_device.subimg_width,
			(int)_device.subimg_height,
			2,
			TextureFormat.RGBA32,
			false
		);
	}

	private void LoadMaterials()
	{
		if (_batchMaterial == null)
		{
			string materialName = "MultiView_sbs_012_27";
			_batchMaterial = Resources.Load<Material>(materialName);
		}

		if (_batchMaterial != null)
		{
			_batchMaterial.SetFloat("_Slope", _device.slope);
			_batchMaterial.SetFloat("_Interval", _device.interval);
			_batchMaterial.SetFloat("_X0", _device.x0);
			_batchMaterial.SetFloat("_OutputSizeX", _device.output_size_X);
			_batchMaterial.SetFloat("_OutputSizeY", _device.output_size_Y);
			_batchMaterial.SetTexture("_TextureArray", textureArray);
			_batchMaterial.SetFloat("_RowNum", _device.rowNum);
			_batchMaterial.SetFloat("_ColNum", _device.colNum);
		}
		else
		{
			Debug.LogError($"Failed to load swizzle material.");
		}

		Shader splitShader = Shader.Find("Hidden/SplitTexture");
		if (splitShader != null)
		{
			_splitMat = new Material(splitShader);
			Debug.Log("Split material loaded successfully");
		}
		else
		{
			Debug.LogError("Failed to load SplitTexture shader");
		}
	}
	#endregion

	#region Cameras
	private void InitTarget()
	{
		Root = new GameObject("Root").transform;
		Target = new GameObject("Target").transform;

		DontDestroyOnLoad(Root);
		DontDestroyOnLoad(Target);
	}

	private void InitCamera()
	{
		_batchcameras = new Camera[2];

		for (int i = 0; i < 2; i++)
		{
			GameObject camObj = useCameraPrefab && BatchCameraPrefab != null
				? Instantiate(BatchCameraPrefab)
				: new GameObject($"BatchCam_{i}");

			_batchcameras[i] = camObj.GetComponent<Camera>() ?? camObj.AddComponent<Camera>();

			DontDestroyOnLoad(camObj);

			_batchcameras[i].usePhysicalProperties = true;
			_batchcameras[i].enabled = true;
			_batchcameras[i].focalLength = _device.f_cam / 100.0f;
			_batchcameras[i].sensorSize = new Vector2(_device.subimg_width / 100.0f, _device.subimg_height / 100.0f);
			_batchcameras[i].targetTexture = cameraRenderTextures[i];
			_batchcameras[i].rect = new Rect(0, 0, 1, 1);
			_batchcameras[i].clearFlags = CameraClearFlags.SolidColor;
			_batchcameras[i].backgroundColor = Color.black;
		}
	}
	#endregion

	#region Update Loop
	private void Update()
	{
			UpdateCameraPositions();
			SplitSBSInput();
			UpdateTextureArray();
			UpdateShader();
	}
	#endregion

	#region Camera Positioning (Critical)
	private void UpdateCameraPositions()
	{
		if (isSBS)
		{
			return;
		}
		float x_fov = FocalPlane * Mathf.Tan(_device.total_fov / 2f * Mathf.Deg2Rad);
		Vector3 upDir = Root.transform.up;
		Vector3 curCamDir = Vector3.Normalize(Target.position - Root.position);
		Vector3 xDir = Vector3.Normalize(Vector3.Cross(curCamDir, upDir));
		Vector3 yDir = Vector3.Normalize(Vector3.Cross(xDir, curCamDir));

		float fl = 1f / (2f * _device.tan_alpha_2);
		float eyeLensShiftX = useEyeTracking ? -(eyeOffset.x * fl) / FocalPlane : 0f;
		float eyeLensShiftY = useEyeTracking ? -(eyeOffset.y * fl * (_device.subimg_width / _device.subimg_height)) / FocalPlane : 0f;

		for (int i = 0; i < _device.total_viewnum; i++)
		{
			_batchcameras[i].orthographic = false;
			_batchcameras[i].usePhysicalProperties = true;

			float x_i = x_fov - (i * 2 * x_fov) / (_device.total_viewnum - 1);
			float a_i = ((x_i * fl) / FocalPlane);

			_batchcameras[i].transform.position = Root.position + xDir * (x_i - eyeOffset.x) + yDir * eyeOffset.y;
			_batchcameras[i].lensShift = new Vector2(a_i + eyeLensShiftX, eyeLensShiftY);
			_batchcameras[i].transform.rotation = Root.rotation;
		}
	}
	#endregion

	#region SBS SPLIT CORE
	private void SplitSBSInput()
	{
		if (_sbsSource == null || _splitMat == null) return;

		RenderTexture.active = cameraRenderTextures[0];
		RenderTexture.active = cameraRenderTextures[1];

		_splitMat.SetFloat("_Half", 0);
		Graphics.Blit(_sbsSource, cameraRenderTextures[0], _splitMat);

		_splitMat.SetFloat("_Half", 1);
		Graphics.Blit(_sbsSource, cameraRenderTextures[1], _splitMat);
	}
	#endregion

	#region Texture Pipeline
	private void UpdateTextureArray()
	{
		for (int i = 0; i < _device.total_viewnum; i++)
		{
			if (i < cameraRenderTextures.Length && cameraRenderTextures[i] != null)
				Graphics.CopyTexture(cameraRenderTextures[i], 0, 0, textureArray, i, 0);
		}
	}

	private void UpdateShader()
	{
		if (_batchMaterial == null) return;

		_batchMaterial.SetFloatArray("_X0Array", X0Array);
		_batchMaterial.SetFloat("_Slope", _device.slope);
		_batchMaterial.SetFloat("_Interval", _device.interval);
		_batchMaterial.SetFloat("_X0", _device.x0);
		_batchMaterial.SetTexture("_TextureArray", textureArray);
		_batchMaterial.SetFloat("_Gamma", 1.0f);
		_batchMaterial.SetFloat("_OutputSizeX", _device.output_size_X);
		_batchMaterial.SetFloat("_OutputSizeY", _device.output_size_Y);
	}
	#endregion

	#region Target / Eye Tracking
	public void ToggleEyeTracking()
	{
		useEyeTracking = !useEyeTracking;
		if (useEyeTracking && _eye != null && !_eye.isRunning)
			_eye.StartTracking();
	}

	public void SetX0Offset(Single value)
	{
		_device.x0 = value;

	}
	#endregion

	#region Data
	[Serializable]
	public class DeviceData
	{
		public int total_viewnum = 2;
		public int imgs_count_x;
		public int imgs_count_y;

		public float total_fov;
		public float sbs_fov;

		public float subimg_width;
		public float subimg_height;

		public float output_size_X;
		public float output_size_Y;

		public float cam_fov_x;
		public float interval_fov;
		public float delta_pos;
		public float delta_700_pos;

		public float rowNum;
		public float colNum;

		public float x0;
		public float interval;
		public float slope;

		public bool is27Layout;
		public float f_cam;
		public float tan_alpha_2;
		public string pixel_order;
	}
	#endregion

	public void QuitApp()
	{
		Application.Quit();
	}
}