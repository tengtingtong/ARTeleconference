/* 
 * Copyright (C) 2015 Christoph Kutza
 * 
 * Please refer to the LICENSE file for license information
 */
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using Byn.Media;
using Byn.Media.Native;

/// <summary>
/// This class + prefab is a complete app allowing to call another app using a shared text or password
/// to meet online.
/// 
/// It supports Audio, Video and Text chat. Audio / Video can optionally turned on/off via toggles.
/// 
/// After the join button is pressed the (first) app will initialize a native webrtc plugin 
/// and contact a server to wait for incoming connections under the given string.
/// 
/// Another instance of the app can connect using the same string. (It will first try to
/// wait for incoming connections which will fail as another app is already waiting and after
/// that it will connect to the other side)
/// 
/// The important methods are "Setup" to initialize the call class (after join button is pressed) and
/// "Call_CallEvent" which reacts to events triggered by the class.
/// 
/// Also make sure to use your own servers for production (uSignalingUrl and uStunServer).
/// 
/// NOTE: Currently, only 1 to 1 connections are supported. This will change in the future.
/// </summary>
public class CallAppUi : MonoBehaviour
{

    /// <summary>
    /// Texture of the local video
    /// </summary>
    protected Texture2D mLocalVideoTexture = null;

    /// <summary>
    /// Texture of the remote video
    /// </summary>
    protected Texture2D mRemoteVideoTexture = null;


    [Header("Setup panel")]
    public RectTransform uSetupPanel;
    public InputField uRoomNameInputField;
    public Button uJoinButton;
    public Dropdown uDeviceDropdown;
    public InputField uIdealWidth;
    public InputField uIdealHeight;
    public InputField uIdealFps;
    public Toggle uRejoinToggle;
    public Button uHolokitButton;
    public Button uBackButton;


    [Header("Video panel elements")]
    /// <summary>
    /// Image of the remote camera
    /// </summary>
    public RawImage uRemoteVideoImage;
    public Transform uMainCamera;

    [Header("Resources")]
    public Texture2D uNoCameraTexture;
    public GameObject FloatingPlaneLeft;
    public GameObject FloatingPlaneLeftDir;
    public GameObject FloatingPlaneRight;
    public GameObject FloatingPlaneRightDir;
    public Shader mChromaKeyShader;

    protected bool mFullscreen = false;


    protected CallApp mApp;


    private float mVideoOverlayTimeout = 0;
    private static readonly float sDefaultOverlayTimeout = 8;

    private int mLocalVideoWidth = -1;
    private int mLocalVideoHeight = -1;
    private int mLocalFps = 0;
    private int mLocalFrameCounter = 0;
    private FramePixelFormat mLocalVideoFormat = FramePixelFormat.Invalid;

    private bool mHasRemoteVideo = false;
    private int mRemoteVideoWidth = -1;
    private int mRemoteVideoHeight = -1;
    private int mRemoteFps = 0;
    private int mRemoteFrameCounter = 0;
    private FramePixelFormat mRemoteVideoFormat = FramePixelFormat.Invalid;

    private float mFpsTimer = 0;

    private string mPrefix = "CallAppUI_";
    private static readonly string PREF_VIDEODEVICE = "videodevice";
    private static readonly string PREF_ROOMNAME = "roomname";
    private static readonly string PREF_IDEALWIDTH = "idealwidth";
    private static readonly string PREF_IDEALHEIGHT = "idealheight";
    private static readonly string PREF_IDEALFPS = "idealfps";
    private static readonly string PREF_REJOIN = "rejoin";

    private bool isInHolokitMode = false;


    protected virtual void Awake()
    {
        mApp = GetComponent<CallApp>();
        mPrefix += this.gameObject.name + "_";
        LoadSettings();
    }

    protected virtual void Start()
    {
        uBackButton.interactable = false;
    }


    private void SaveSettings()
    {
        PlayerPrefs.SetString(mPrefix + PREF_VIDEODEVICE, GetSelectedVideoDevice());
        PlayerPrefs.SetString(mPrefix + PREF_ROOMNAME, uRoomNameInputField.text);
        PlayerPrefs.SetString(mPrefix + PREF_IDEALWIDTH, uIdealWidth.text);
        PlayerPrefs.SetString(mPrefix + PREF_IDEALHEIGHT, uIdealHeight.text);
        PlayerPrefs.SetString(mPrefix + PREF_IDEALFPS, uIdealFps.text);
        PlayerPrefsSetBool(mPrefix + PREF_REJOIN, uRejoinToggle.isOn);
        PlayerPrefs.Save();
    }

    private string mStoredVideoDevice = null;

    /// <summary>
    /// Loads the ui state from last use
    /// </summary>
    private void LoadSettings()
    {

        //can't select this immediately because we don't know if it is valid yet
        mStoredVideoDevice = PlayerPrefs.GetString(mPrefix + PREF_VIDEODEVICE, null);
        uRoomNameInputField.text = PlayerPrefs.GetString(mPrefix + PREF_ROOMNAME, uRoomNameInputField.text);
        uIdealWidth.text = PlayerPrefs.GetString(mPrefix + PREF_IDEALWIDTH, "320");
        uIdealHeight.text = PlayerPrefs.GetString(mPrefix + PREF_IDEALHEIGHT, "240");
        uIdealFps.text = PlayerPrefs.GetString(mPrefix + PREF_IDEALFPS, "30");
        uRejoinToggle.isOn = PlayerPrefsGetBool(mPrefix + PREF_REJOIN, false);
    }

    private static bool PlayerPrefsGetBool(string name, bool defval)
    {
        int def = 0;
        if (defval)
            def = 1;
        return PlayerPrefs.GetInt(name, def) == 1 ? true : false;
    }

    private static void PlayerPrefsSetBool(string name, bool value)
    {
        PlayerPrefs.SetInt(name, value ? 1 : 0);
    }

    private string GetSelectedVideoDevice()
    {
        if (uDeviceDropdown.value <= 0 || uDeviceDropdown.value >= uDeviceDropdown.options.Count)
        {
            return null;
        }
        else
        {
            string devname = uDeviceDropdown.options[uDeviceDropdown.value].text;
            return devname;
        }
    }

    private static int TryParseInt(string value, int defval)
    {
        int result;
        if (int.TryParse(value, out result) == false)
        {
            result = defval;
        }
        return result;
    }

    private void SetupCallApp()
    {
        mApp.SetVideoDevice(GetSelectedVideoDevice());

        //Set true to allow sending video and audio to other connections
        mApp.SetAudio(false);
        mApp.SetVideo(false);

        int width = TryParseInt(uIdealWidth.text, 320);
        int height = TryParseInt(uIdealHeight.text, 240);
        int fps = TryParseInt(uIdealFps.text, 320);
        mApp.SetIdealResolution(width, height);
        mApp.SetIdealFps(fps);
        mApp.SetAutoRejoin(uRejoinToggle.isOn);
        mApp.SetupCall();
        EnsureLength();
        Append("Trying to listen on address " + uRoomNameInputField.text);
        mApp.Join(uRoomNameInputField.text);
    }

    /// <summary>
    /// Updates the remote video. If the frame is null it will hide the video image.
    /// </summary>
    /// <param name="frame"></param>
    public virtual void UpdateRemoteTexture(IFrame frame, FramePixelFormat format)
    {
        if (uRemoteVideoImage != null)
        {
            if (frame != null)
            {
                UnityMediaHelper.UpdateTexture(frame, ref mRemoteVideoTexture);
                //Implement Video texture in UI mode
                uRemoteVideoImage.texture = mRemoteVideoTexture;
                uRemoteVideoImage.transform.rotation = Quaternion.Euler(new Vector3(0, 0, frame.Rotation * -1));

                //Implement Video texture in Holokit mode
                FloatingPlaneLeft.GetComponent<Renderer>().material.mainTexture = mRemoteVideoTexture;
                FloatingPlaneRight.GetComponent<Renderer>().material.mainTexture = mRemoteVideoTexture;
                FloatingPlaneLeft.GetComponent<Renderer>().material.shader = mChromaKeyShader;
                FloatingPlaneRight.GetComponent<Renderer>().material.shader = mChromaKeyShader;

                //Shift texture for Lefteye_plane & Righteye_plane
                FloatingPlaneLeft.GetComponent<Renderer>().material.mainTextureScale = new Vector2(0.5f, 1);
                FloatingPlaneRight.GetComponent<Renderer>().material.mainTextureScale = new Vector2(0.5f, 1);
                FloatingPlaneRight.GetComponent<Renderer>().material.mainTextureOffset = new Vector2(0.5f, 0);
                

                //FloatingPlaneLeft.gameObject.transform.Rotate(0, 0, 180);
                //FloatingPlaneRight.gameObject.transform.Rotate(0, 0, 180);


                mHasRemoteVideo = true;
                mRemoteVideoWidth = frame.Width;
                mRemoteVideoHeight = frame.Height;
                mRemoteVideoFormat = format;
                mRemoteFrameCounter++;
            }
            else
            {
                mHasRemoteVideo = false;
                uRemoteVideoImage.texture = uNoCameraTexture;
                FloatingPlaneLeft.GetComponent<Renderer>().material.mainTexture = uNoCameraTexture;
                FloatingPlaneRight.GetComponent<Renderer>().material.mainTexture = uNoCameraTexture;
            }
        }
    }


    /// <summary>
    /// Updates the dropdown menu based on the current video devices and toggle status
    /// </summary>
    public void UpdateVideoDropdown()
    {
        uDeviceDropdown.ClearOptions();
        uDeviceDropdown.AddOptions(new List<string>(mApp.GetVideoDevices()));
        uDeviceDropdown.interactable = mApp.CanSelectVideoDevice();

        //restore the stored selection if possible
        if(uDeviceDropdown.interactable  && mStoredVideoDevice != null)
        {
            int index = 0;
            foreach(var opt in uDeviceDropdown.options)
            {
                if(opt.text == mStoredVideoDevice)
                {
                    uDeviceDropdown.value = index;
                }
                index++;
            }
        }
    }
    public void VideoDropdownOnValueChanged(int index)
    {
        //moved to SetupCallApp
    }


    /// <summary>
    /// Adds a new message to the message view
    /// </summary>
    /// <param name="text"></param>
    public void Append(string text)
    {
        Debug.Log("Chat output: " + text);
    }

    /// <summary>
    /// Shows the setup screen or the chat + video
    /// </summary>
    /// <param name="showSetup">true Shows the setup. False hides it.</param>
    public void SetGuiState(bool showSetup)
    {
        Text txtJoinButton = uJoinButton.GetComponentInChildren<Text>();
        //this is going to hide the textures until it is updated with a new frame update
        UpdateRemoteTexture(null, FramePixelFormat.Invalid);

        uHolokitButton.interactable = !showSetup;

        uRoomNameInputField.interactable = showSetup;
        uDeviceDropdown.interactable = showSetup;
        uRejoinToggle.interactable = showSetup;
        uIdealWidth.interactable = showSetup;
        uIdealHeight.interactable = showSetup;
        uIdealFps.interactable = showSetup;

        if (showSetup)
        {
            txtJoinButton.text = "Join";
        }
        else
        {
            txtJoinButton.text = "Stop";
        }
    }

    /// <summary>
    /// Join button pressed. Tries to join a room.
    /// And press again to unjoin a room
    /// </summary>
    private bool joinButtonState = true; 
    public void JoinButtonPressed()
    {    
        if (joinButtonState)
        {
            SaveSettings();
            SetupCallApp();
        }
        else
        {
            mApp.ResetCall();
        }
        joinButtonState = !joinButtonState;
    }

    private void EnsureLength()
    {
        if (uRoomNameInputField.text.Length > CallApp.MAX_CODE_LENGTH)
        {
            uRoomNameInputField.text = uRoomNameInputField.text.Substring(0, CallApp.MAX_CODE_LENGTH);
        }
    }

    public string GetRoomname()
    {
        EnsureLength();
        return uRoomNameInputField.text;
    }

    /// <summary>
    /// HolokitMode button pressed. 
    /// Change Scene to "HolokitScene"
    /// </summary>
    public void HolokitModeButtonPressed()
    {
        uSetupPanel.gameObject.SetActive(!uSetupPanel.gameObject.activeSelf);
        isInHolokitMode = true;
        uBackButton.interactable = true;
    }

    /// <summary>
    /// Back button pressed. 
    /// Change Scene to "WebrtcScene"
    /// </summary>
    public void BackButtonPressed()
    {
        uSetupPanel.gameObject.SetActive(!uSetupPanel.gameObject.activeSelf);
        mApp.ResetCall();
        joinButtonState = true;
        isInHolokitMode = false;
        uBackButton.interactable = false;
    }

    
    protected virtual void Update()
    {
        FloatingPlaneLeftDir.gameObject.transform.LookAt(uMainCamera);
        FloatingPlaneRightDir.gameObject.transform.LookAt(uMainCamera);
        if (mVideoOverlayTimeout > 0)
        { 
            string remote = "Remote:";
            if (mHasRemoteVideo == false)
            {
                remote += "no video";
            }
            else
            {
                remote += mRemoteVideoWidth + "x" + mRemoteVideoHeight + Enum.GetName(typeof(FramePixelFormat), mRemoteVideoFormat) + " FPS:" + mRemoteFps;
            }

            mVideoOverlayTimeout -= Time.deltaTime;
            if(mVideoOverlayTimeout <= 0)
            {
                mVideoOverlayTimeout = 0;
            }
        }

        float fpsTimeDif = Time.realtimeSinceStartup - mFpsTimer;
        if(fpsTimeDif > 1)
        {
            mRemoteFps = Mathf.RoundToInt(mRemoteFrameCounter / fpsTimeDif);
            mFpsTimer = Time.realtimeSinceStartup;
            mRemoteFrameCounter = 0;
        }
    }
}
