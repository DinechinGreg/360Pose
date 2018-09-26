using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Animations;
using Valve.VR.InteractionSystem;

public class PlayAnimation : MonoBehaviour 
{
	public GameObject _avatarFBX;
	public TextAsset _jointReg;
	public TextAsset _shape;
	public Material _avatarMaterial;
	public Texture2D _backgroundTexture;
	public AnimationClip _videoAnimationClip;
	public AnimationClip _idleClip;
	public AudioClip _audioClip;

	private float _groundHeight = -1.5f;
	private Camera _mainCamera;
	private Transform _neckTransform;
	private Transform _armatureTransform;
	private Transform _leftFootTransform;
	private Transform _rightFootTransform;
	private Animator _animator;
	private AudioSource _audioSource;
	private bool _isIdle;
	private float _videoClipNormalizedTime;

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	IEnumerator Start () 
	{
		_mainCamera = Camera.main;
		_mainCamera.enabled = false;
		yield return StartCoroutine (CreateSphereCoroutine ());
		yield return StartCoroutine (CreateAvatarCoroutine ());
		_mainCamera.enabled = true;
		Player.instance.transform.position -= _mainCamera.transform.position;
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	void Update()
	{
		if (!_isIdle && _animator != null) 
		{
			_videoClipNormalizedTime += Time.deltaTime / _videoAnimationClip.length;
			_animator.SetFloat ("NormalizedTime", _videoClipNormalizedTime);
		}
		if (Input.GetKeyDown (KeyCode.Space)) 
		{
			SwitchIdle (!_isIdle);
		}
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	void LateUpdate()
	{
		if (_leftFootTransform != null && _rightFootTransform != null) 
		{
			float lowestFootY = Mathf.Min (_leftFootTransform.position.y, _rightFootTransform.position.y) - 0.075f;
			_armatureTransform.position += Mathf.Max (0, _groundHeight - lowestFootY) * Vector3.up;
		}
		if (_mainCamera != null && _neckTransform != null) 
		{
			_neckTransform.LookAt (_mainCamera.transform.position - 0.1f * _neckTransform.up);
		}
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private IEnumerator CreateSphereCoroutine()
	{
		Texture2D backgroundCol = _backgroundTexture;
		Material mat = new Material (Shader.Find ("Custom/BackgroundDisplaySphereShader"));
		mat.SetTexture ("_MainTex", backgroundCol);
		GameObject deformableSphereGO = new GameObject ("Background");
		DeformableSphere deformableSphere = deformableSphereGO.AddComponent<DeformableSphere> ();
		deformableSphere.Generate (backgroundCol.width, backgroundCol.height/2, 10f, mat, null);
		while (deformableSphere._isCreatingMesh)
			yield return null;
		deformableSphereGO.transform.RotateAround (Vector3.zero, Vector3.up, -90f);
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private AnimatorController GenerateAnimatorController()
	{
		AnimatorController animController = AnimatorController.CreateAnimatorControllerAtPath("Assets/Resources/Animation/VideoAnimatorController.controller");
		animController.AddParameter ("SwitchIdle", AnimatorControllerParameterType.Bool);
		animController.AddParameter ("NormalizedTime", AnimatorControllerParameterType.Float);
		AnimatorStateMachine rootStateMachine = animController.layers[0].stateMachine;
		AnimatorState videoClipState = rootStateMachine.AddState ("VideoAnimation");
		videoClipState.motion = _videoAnimationClip;
		videoClipState.timeParameterActive = true;
		videoClipState.timeParameter = "NormalizedTime";
		videoClipState.writeDefaultValues = false;
		AnimatorState idleClipState = rootStateMachine.AddState ("Idle");
		idleClipState.motion = _idleClip;
		idleClipState.writeDefaultValues = false;
		AnimatorStateTransition videoToIdle = videoClipState.AddTransition (idleClipState, false);
		videoToIdle.AddCondition (AnimatorConditionMode.If, 0, "SwitchIdle");
		AnimatorStateTransition idleToVideo = idleClipState.AddTransition (videoClipState, false);
		idleToVideo.AddCondition (AnimatorConditionMode.IfNot, 0, "SwitchIdle");
		AnimatorStateTransition idleToIdle = idleClipState.AddTransition (idleClipState, true);
		return animController;
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private IEnumerator CreateAvatarCoroutine()
	{
		GameObject avatar = GameObject.Instantiate (_avatarFBX);
		SMPLBlendshapes blendshapes = avatar.GetComponentInChildren<Renderer>().gameObject.AddComponent<SMPLBlendshapes> ();
		blendshapes.jointRegressorJSON = _jointReg;
		blendshapes.shapeParmsJSON = _shape;
		SkinnedMeshRenderer smRenderer = avatar.GetComponentInChildren<SkinnedMeshRenderer> ();
		smRenderer.updateWhenOffscreen = true;
		smRenderer.material = _avatarMaterial;
		_animator = avatar.GetComponent<Animator> ();
		_animator.runtimeAnimatorController = GenerateAnimatorController ();
		while (_animator.GetCurrentAnimatorStateInfo (0).normalizedTime <= 0f)
			yield return null;
		_armatureTransform = GameObject.Find("Armature").transform;
		_leftFootTransform = GameObject.Find("m_avg_L_Foot").transform;
		_rightFootTransform = GameObject.Find("m_avg_R_Foot").transform;
		_neckTransform = GameObject.Find("m_avg_Neck").transform;
		_audioSource = _neckTransform.gameObject.AddComponent<AudioSource> ();
		_audioSource.clip = _audioClip;
		_audioSource.Play ();
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private void SwitchIdle(bool toIdle)
	{
		_animator.SetBool ("SwitchIdle", toIdle);
		if (toIdle)
			_audioSource.Pause ();
		else
			_audioSource.UnPause ();
		_isIdle = toIdle;
	}
}
