using Cubevi_Swizzle;
using UnityEngine;

public class SBSCaptureManager : MonoBehaviour
{
	[Header("Capture Quad References")]
	public Renderer leftCaptureQuad;
	public Renderer rightCaptureQuad;
	public Material captureMaterial;

	[Header("Object References")]
	public GameObject cubeGroupToHide;

	[Header("Camera References")]
	private BatchCameraManager batchCameraManager;

	private bool isCaptureActive = false;
	private RenderTexture currentCaptureTexture;

	private Vector3 InitialquadScale;
	private bool isInitialized = false;

	private void Start()
	{
		InitializeVideoComponents();
	}

	public void Initialize()
	{
		if (batchCameraManager == null)
			batchCameraManager = FindObjectOfType<BatchCameraManager>();
	}

	private void InitializeVideoComponents()
	{
		if (batchCameraManager == null)
			batchCameraManager = FindObjectOfType<BatchCameraManager>();

		if (batchCameraManager == null)
		{
			Debug.LogError("SBSCaptureManager: BatchCameraManager not found!");
			return;
		}

		if (leftCaptureQuad == null || rightCaptureQuad == null)
			CreateCaptureQuads();

		// EXACT MATCH: SBSVideoManager line 83
		InitialquadScale = leftCaptureQuad.transform.localScale;

		leftCaptureQuad.gameObject.SetActive(false);
		rightCaptureQuad.gameObject.SetActive(false);

		isInitialized = true;

		Debug.Log("SBSCaptureManager initialized. InitialquadScale: " + InitialquadScale);
	}

	private void CreateCaptureQuads()
	{
		GameObject quadParent = new GameObject("SBSCaptureQuads");
		quadParent.transform.SetParent(transform, false);

		GameObject leftQuadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
		leftQuadObj.name = "LeftCaptureQuad";
		leftQuadObj.transform.SetParent(quadParent.transform, false);

		GameObject rightQuadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
		rightQuadObj.name = "RightCaptureQuad";
		rightQuadObj.transform.SetParent(quadParent.transform, false);

		Destroy(leftQuadObj.GetComponent<Collider>());
		Destroy(rightQuadObj.GetComponent<Collider>());

		leftCaptureQuad = leftQuadObj.GetComponent<Renderer>();
		rightCaptureQuad = rightQuadObj.GetComponent<Renderer>();

		leftQuadObj.transform.localPosition = new Vector3(-9.5f, 0f, 0f);
		rightQuadObj.transform.localPosition = new Vector3(9.5f, 0f, 0f);


		if (captureMaterial != null)
		{
			leftCaptureQuad.material = new Material(captureMaterial);
			rightCaptureQuad.material = new Material(captureMaterial);
		}
		else
		{
			Shader shader = Shader.Find("Unlit/Texture");
			leftCaptureQuad.material = new Material(shader);
			rightCaptureQuad.material = new Material(shader);
		}
	}

	private void AdjustUVs()
	{
		MeshFilter leftMeshFilter = leftCaptureQuad.GetComponent<MeshFilter>();
		MeshFilter rightMeshFilter = rightCaptureQuad.GetComponent<MeshFilter>();

		if (leftMeshFilter == null || rightMeshFilter == null)
		{
			Debug.LogError("MeshFilter component not found.");
			return;
		}

		Mesh leftMesh = leftMeshFilter.mesh;
		Mesh rightMesh = rightMeshFilter.mesh;

		Vector2[] leftUVs = new Vector2[]
		{
		new Vector2(0, 1),  
        new Vector2(0.5f, 1),
        new Vector2(0, 0),   
        new Vector2(0.5f, 0)
		};
		leftMesh.uv = leftUVs;

		Vector2[] rightUVs = new Vector2[]
		{
		new Vector2(0.5f, 1),   
        new Vector2(1, 1),        
        new Vector2(0.5f, 0),
        new Vector2(1, 0) 
		};
		rightMesh.uv = rightUVs;
	}

	public void StartCapture(RenderTexture captureTexture)
	{
		if (!isInitialized)
		{
			Debug.LogError("SBSCaptureManager not initialized!");
			return;
		}

		if (isCaptureActive)
			StopCapture();

		if (captureTexture == null)
		{
			Debug.LogError("Capture texture is null");
			return;
		}

		if (batchCameraManager != null && batchCameraManager.GetBatchCameras() != null && batchCameraManager.GetBatchCameras().Length == 2)
		{
			Texture videoTexture = captureTexture;
			if (videoTexture == null)
			{
				Debug.LogError("Texture not assigned or loaded.");
				return;
			}

			int width = videoTexture.width;
			int height = videoTexture.height;

			currentCaptureTexture = captureTexture;

			leftCaptureQuad.material.mainTexture = videoTexture;
			rightCaptureQuad.material.mainTexture = videoTexture;

			float videoAspectRatio = (float)width / height;

			Vector3 NewQuadScale = InitialquadScale;
			NewQuadScale.x = (videoAspectRatio / 2) * NewQuadScale.y;

			leftCaptureQuad.transform.localScale = NewQuadScale;
			rightCaptureQuad.transform.localScale = NewQuadScale;

			batchCameraManager.GetBatchCameras()[0].transform.position = new Vector3(-9.5f, 0, 0);
			batchCameraManager.GetBatchCameras()[0].orthographic = true;
			batchCameraManager.GetBatchCameras()[0].orthographicSize = 2.25f;
			batchCameraManager.GetBatchCameras()[1].transform.position = new Vector3(9.5f, 0, 0);
			batchCameraManager.GetBatchCameras()[1].orthographic = true;
			batchCameraManager.GetBatchCameras()[1].orthographicSize = 2.25f;

			AdjustUVs();

			leftCaptureQuad.gameObject.SetActive(true);
			rightCaptureQuad.gameObject.SetActive(true);

			isCaptureActive = true;

			if (batchCameraManager != null)
				batchCameraManager.SetSBSMode(true);

			HideSceneObjects(true);

			Debug.Log("Capture started: " + width + "x" + height);
		}
		else
		{
			Debug.LogError("BatchCameraManager or cameras not properly set up");
		}
	}

	public void UpdateCaptureTexture(RenderTexture newTexture)
	{
		if (!isCaptureActive || newTexture == null)
			return;

		currentCaptureTexture = newTexture;

		leftCaptureQuad.material.mainTexture = newTexture;
		rightCaptureQuad.material.mainTexture = newTexture;
	}

	public void UpdateResolution(int width, int height)
	{
		if (!isCaptureActive)
			return;

		float videoAspectRatio = (float)width / height;

		Vector3 NewQuadScale = InitialquadScale;
		NewQuadScale.x = videoAspectRatio * NewQuadScale.y;
		leftCaptureQuad.transform.localScale = NewQuadScale;
		rightCaptureQuad.transform.localScale = NewQuadScale;
	}

	private void HideSceneObjects(bool hide)
	{
		if (cubeGroupToHide != null)
			cubeGroupToHide.SetActive(!hide);
	}

	public void StopCapture()
	{
		if (!isCaptureActive)
			return;

		leftCaptureQuad.gameObject.SetActive(false);
		rightCaptureQuad.gameObject.SetActive(false);

		isCaptureActive = false;

		if (batchCameraManager != null)
			batchCameraManager.SetSBSMode(false);

		HideSceneObjects(false);

		Debug.Log("Capture stopped");
	}

	public bool IsCaptureActive()
	{
		return isCaptureActive;
	}
}