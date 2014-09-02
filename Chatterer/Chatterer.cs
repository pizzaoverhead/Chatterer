///////////////////////////////////////////////////////////////////////////////
//
//    Chatterer a plugin for Kerbal Space Program from SQUAD
//    (https://www.kerbalspaceprogram.com/)
//    Copyright (C) 2014 Athlonic 
//    (original work and with permission from : Iannic-ann-od)
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
///////////////////////////////////////////////////////////////////////////////



/* DO ME
 * 
 * FIX EVA detection (avoid switching vessel to trigger Airlock sound)
 * FIX the mute all function
 * 
 * ADD Applauncher/BlizzyTB button behaviour/animations (Idle, disabled, muted, onChat, ...)
 * 
 * Separate the code in different .cs files accordingly to their function
 * 
 * //
 * 
 * ADD some fillable Preset slots to store configured chatter/beeps/filters for later use (single beep, all beeps, or all audio)
 * save all clipboard nodes to disk when any are filled/changed
 * load them at start and vessel switch
 * 
 * 
 * //ADD a settings 'clipboard' to copy/paste current single beepsource settings to another beepsource
 * //ADD EVA-capsule chatter (if nearby crew > 0) and capsule-capsule chatter (if vessel crew > 1)
 *  
 */


///////////////////////////////////////////////////////////////////////////////


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;

namespace Chatterer
{
    public class ChatterAudioList
    {
        //class to manage chatter clips
        public List<AudioClip> capcom;
        public List<AudioClip> capsule;
        public string directory;
        public bool is_active;

        public ChatterAudioList()
        {
            capcom = new List<AudioClip>();
            capsule = new List<AudioClip>();
            directory = "dir";
            is_active = true;
        }
    }

    public class BeepSource
    {
        //class to manage beeps
        public GameObject beep_player;
        public string beep_name;
        public AudioSource audiosource;
        public Rect settings_window_pos;
        public bool show_settings_window;
        public int settings_window_id;
        public bool precise;
        public float precise_freq_slider;
        public int precise_freq;
        public int prev_precise_freq;
        public float loose_freq_slider;
        public int loose_freq;
        public int prev_loose_freq;
        public int loose_timer_limit;
        public float timer;
        public string current_clip;
        public int sel_filter;
        public AudioChorusFilter chorus_filter;
        public AudioDistortionFilter distortion_filter;
        public AudioEchoFilter echo_filter;
        public AudioHighPassFilter highpass_filter;
        public AudioLowPassFilter lowpass_filter;
        public AudioReverbFilter reverb_filter;
        public AudioReverbPreset reverb_preset;
        public int reverb_preset_index;

        public BeepSource()
        {
            settings_window_pos = new Rect(Screen.width / 2f, Screen.height / 2f, 10f, 10f);
            show_settings_window = false;
            precise = true;
            precise_freq_slider = -1f;
            precise_freq = -1;
            prev_precise_freq = -1;
            loose_freq_slider = 0;
            loose_freq = 0;
            prev_loose_freq = 0;
            loose_timer_limit = 0;
            timer = 0;
            sel_filter = 0;
            reverb_preset_index = 0;
        }
    }

    public class BackgroundSource
    {
        //class to manage background audiosources
        public GameObject background_player;
        public string name;
        public AudioSource audiosource;
        public string current_clip;
    }

    //public class SoundscapeSource
    //{
    //    public GameObject soundscape_player;
    //    public string name;
    //    public AudioSource audiosource;
    //    public string current_clip;
    //}

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class chatterer : MonoBehaviour
    {
        private static System.Random rand = new System.Random();

        private int window_base_id = -12381578;

        private bool debugging = false;      //lots of extra log info if true

        private Vessel vessel;          //is set to FlightGlobals.ActiveVessel
        private Vessel prev_vessel;     //to detect change in active vessel

        //GameObjects to hold AudioSources and AudioFilters
        //private GameObject musik_player = new GameObject();
        private GameObject chatter_player = new GameObject();
        private GameObject sstv_player = new GameObject();

        //Chatter AudioSources
        private AudioSource initial_chatter = new AudioSource();
        private AudioSource response_chatter = new AudioSource();
        private AudioSource quindar1 = new AudioSource();
        private AudioSource quindar2 = new AudioSource();
        //private AudioSource musik = new AudioSource();

        //SSTV AudioSources
        private AudioSource sstv = new AudioSource();

        //All beep objects, audiosources, and filters are managed by BeepSource class
        private List<BeepSource> beepsource_list = new List<BeepSource>();     //List to hold the BeepSources
        private List<BackgroundSource> backgroundsource_list = new List<BackgroundSource>();    //list to hold the BackgroundSources
        
        //Chatter, SSTV, and beep audio sample Lists and Dictionaries
        private List<ChatterAudioList> chatter_array = new List<ChatterAudioList>();        //array of all chatter clips and some settings
        private Dictionary<string, AudioClip> dict_probe_samples = new Dictionary<string, AudioClip>();
        private Dictionary<AudioClip, string> dict_probe_samples2 = new Dictionary<AudioClip, string>();
        private List<AudioClip> all_sstv_clips = new List<AudioClip>();
        private Dictionary<string, AudioClip> dict_background_samples = new Dictionary<string, AudioClip>();
        private Dictionary<AudioClip, string> dict_background_samples2 = new Dictionary<AudioClip, string>();
        private Dictionary<string, AudioClip> dict_soundscape_samples = new Dictionary<string, AudioClip>();
        private Dictionary<AudioClip, string> dict_soundscape_samples2 = new Dictionary<AudioClip, string>();

        //Chatter audio lists
        private List<AudioClip> current_capcom_chatter = new List<AudioClip>();     //holds chatter of toggled sets
        private List<AudioClip> current_capsule_chatter = new List<AudioClip>();    //one of these becomes initial, the other response
        private int current_capcom_clip;
        private int current_capsule_clip;

        private AudioClip quindar_clip;

        //Chatter variables
        private bool exchange_playing = false;
        private bool response_chatter_started = false;
        private bool pod_begins_exchange = false;
        private int initial_chatter_source; //whether capsule or capcom begins exchange
        private List<AudioClip> initial_chatter_set = new List<AudioClip>();    //random clip pulled from here
        private int initial_chatter_index;  //index of random clip
        private List<AudioClip> response_chatter_set = new List<AudioClip>();   //and here
        private int response_chatter_index;
        private int response_delay_secs;

        //GUI
        private bool gui_running = false;
        private int skin_index = 0;     //selected skin
        private bool gui_styles_set = false;
        private bool hide_all_windows = true;
        private string custom_dir_name = "directory name";  //default text for audioset input box
        private int active_menu = 0;    //selected main window section (sliders, sets, etc)
        private int chatter_sel_filter;     //currently selected filter in filters window
        private int sel_beep_src = 0;   //currently selected beep source
        private int sel_beep_page = 1;
        private int num_beep_pages;
        private int prev_num_pages;

        //integration with blizzy78's Toolbar plugin
        private ToolbarButtonWrapper chatterer_toolbar_button;
        private bool useBlizzy78Toolbar = false;

        //KSP Stock application launcherButton
        private ApplicationLauncherButton launcherButton = null;
        private Texture2D chatterer_button_Texture = null;
        private Texture2D chatterer_button_TX = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_TX_muted = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_RX = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_RX_muted = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_SSTV = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_SSTV_muted = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_idle = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_idle_muted = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        //private Texture2D chatterer_button_disabled = new Texture2D(38, 38, TextureFormat.ARGB32, false); //for later RT2 use
        //private Texture2D chatterer_button_disabled_muted = new Texture2D(38, 38, TextureFormat.ARGB32, false);

        //Main window
        protected Rect main_window_pos = new Rect(Screen.width / 2f, Screen.height / 2f, 10f, 10f);

        //Chatter filters window
        private bool show_chatter_filter_settings = false;
        protected Rect chatter_filter_settings_window_pos = new Rect(Screen.width / 2, Screen.height / 2, 10f, 10f);
        private int chatter_filter_settings_window_id;

        //Probe Sample Selector window
        private Vector2 probe_sample_selector_scroll_pos = new Vector2();
        private bool show_probe_sample_selector = false;
        protected Rect probe_sample_selector_window_pos = new Rect(Screen.width / 2f, Screen.height / 2f, 10f, 10f);
        private int probe_sample_selector_window_id;

        //Background AAE Selector window
        private Vector2 AAE_background_sample_selector_scroll_pos = new Vector2();
        private bool show_AAE_background_sample_selector = false;
        protected Rect AAE_background_sample_selector_window_pos = new Rect(Screen.width / 2f, Screen.height / 2f, 10f, 10f);
        private int AAE_background_sample_selector_window_id;

        //The Lab window
        private bool show_lab_gui = false;
        protected Rect lab_window_pos = new Rect(Screen.width / 2f, Screen.height / 2f, 10f, 10f);
        private int lab_window_id;

        //Textures
        //private Texture2D ui_icon_off = new Texture2D(30, 30, TextureFormat.ARGB32, false);
        //private Texture2D ui_icon_on = new Texture2D(30, 30, TextureFormat.ARGB32, false);
        //private Texture2D ui_icon = new Texture2D(30, 30, TextureFormat.ARGB32, false);
        private Texture2D line_512x4 = new Texture2D(512, 8, TextureFormat.ARGB32, false);

        //GUIStyles
        private GUIStyle label_txt_left;
        private GUIStyle label_txt_center;
        private GUIStyle label_txt_right;
        private GUIStyle label_txt_red_center;
        private GUIStyle button_txt_left;
        private GUIStyle button_txt_right;
        private GUIStyle button_txt_center;
        //private GUIStyle button_txt_center_green;
        private GUIStyle gs_tooltip;
        //private GUIStyle xkcd_label;
        private GUIStyle label_txt_bold;
        private GUIStyle button_txt_left_bold;

        //Plugin settings
        private bool run_once = true;   //used to run some things just once in Update() that don't get done in Awake()
        //private bool power_available = true;
        private bool quindar_toggle = true;
        private bool disable_beeps_during_chatter = true;
        private bool remotetech_toggle = false;
        //private bool disable_power_usage = false;
        private bool show_tooltips = true;
        private bool http_update_check = false;
        private bool use_vessel_settings = false;
        private bool prev_use_vessel_settings = false;

        //Chatter filters
        private AudioChorusFilter chatter_chorus_filter;
        private AudioDistortionFilter chatter_distortion_filter;
        private AudioEchoFilter chatter_echo_filter;
        private AudioHighPassFilter chatter_highpass_filter;
        private AudioLowPassFilter chatter_lowpass_filter;
        private AudioReverbFilter chatter_reverb_filter;
        private int chatter_reverb_preset_index = 0;

        //Counters
        private float rt_update_timer = 0;
        private float sstv_timer = 0;
        private float sstv_timer_limit = 0;
        private float secs_since_last_exchange = 0;
        private float secs_since_initial_chatter = 0;
        private float secs_between_exchanges = 0;

        //Sliders
        private float chatter_freq_slider = 3f;
        private int chatter_freq = 3;
        private int prev_chatter_freq = 3;
        private float chatter_vol_slider = 0.5f;
        private float prev_chatter_vol_slider = 0.5f;

        private float quindar_vol_slider = 0.5f;
        private float prev_quindar_vol_slider = 0.5f;

        private float sstv_freq_slider = 0;
        private int sstv_freq = 0;
        private int prev_sstv_freq = 0;
        private float sstv_vol_slider = 0.25f;
        private float prev_sstv_vol_slider = 0.25f;

        //Insta-chatter key
        private KeyCode insta_chatter_key = KeyCode.Slash;
        private bool set_insta_chatter_key = false;
        private bool insta_chatter_key_just_changed = false;

        //Insta-SSTV key
        private KeyCode insta_sstv_key = KeyCode.Quote;
        private bool set_insta_sstv_key = false;
        private bool insta_sstv_key_just_changed = false;

        //these are updated and trigger chatter when changed
        private Vessel.Situations vessel_prev_sit;
        private int vessel_prev_stage;
        private int vessel_part_count;

        //RemoteTech
        bool
            //whether the vessel has a RemoteTech SPU
            hasRemoteTech = false,

            //whether the RemoteTech flight computer is controlling attitude
            attitudeActive = false,

            //whether local control is active, meaning no control delays
            //localControl = false,

            //whether the vessel is in radio contact
            inRadioContact = false;

        double
            //the current signal delay (is returned as 0 if the vessel is not in contact)
            controlDelay = 0;

        //Version
        private string this_version = "0.6.3.86";
        private string main_window_title = "Chatterer ";
        //private string latest_version = "";
        //private bool recvd_latest_version = false;

        //Clipboards
        private ConfigNode filters_clipboard;
        private ConfigNode chorus_clipboard;
        private ConfigNode dist_clipboard;
        private ConfigNode echo_clipboard;
        private ConfigNode hipass_clipboard;
        private ConfigNode lopass_clipboard;
        private ConfigNode reverb_clipboard;
        private ConfigNode beepsource_clipboard;

        //Settings nodes
        private string settings_path;
        private ConfigNode plugin_settings_node;
        private ConfigNode vessel_settings_node;

        //Unsorted
        private BeepSource OTP_source = new BeepSource();
        private AudioClip OTP_stored_clip;    //holds the set probe sample while another sample plays once
        private bool OTP_playing = false;

        private ConfigNode filter_defaults;

        private bool mute_all = false;
        private bool all_muted = false;

        private bool show_advanced_options = false;
        private bool show_chatter_sets = false;


        private bool chatter_exists = false;
        private bool sstv_exists = false;
        private bool beeps_exists = false;


        private string menu = "chatter";    //default to chatter menu because it has to have something

        private List<GUISkin> g_skin_list;

        private string yep_yep = "";
        private bool yep_yep_loaded = false;


        //AAE
        //private bool AAE_exists = false;

        private bool aae_backgrounds_exist = false;
        private bool aae_soundscapes_exist = false;
        private bool aae_breathing_exist = false;
        private bool aae_airlock_exist = false;
        private bool aae_wind_exist = false;


        private GameObject aae_soundscape_player = new GameObject();
        private AudioSource aae_soundscape = new AudioSource();

        private GameObject aae_ambient_player = new GameObject();
        private AudioSource aae_breathing = new AudioSource();
        private AudioSource aae_airlock = new AudioSource();
        private AudioSource aae_wind = new AudioSource();
        private float aae_wind_vol_slider = 1.0f;

        private BackgroundSource sel_background_src;    //so sample selector window knows which backgroundsource we are working with


        private int aae_soundscape_freq = 0;
        private int aae_prev_soundscape_freq = 0;
        private float aae_soundscape_freq_slider = 2;
        private float aae_soundscape_timer = 0;
        private float aae_soundscape_timer_limit = 0;
        private string aae_soundscape_current_clip = "";


        private AudioSource landingsource = new AudioSource();
        private AudioSource yep_yepsource = new AudioSource();



        //////////////////////////////////////////////////
        //////////////////////////////////////////////////


        //GUI

        internal chatterer() 
        {
            //integration with blizzy78's Toolbar plugin
            if (ToolbarButtonWrapper.ToolbarManagerPresent)
            {
                if (debugging) Debug.Log("[CHATR] blizzy78's Toolbar plugin found ! Set toolbar button.");

                chatterer_toolbar_button = ToolbarButtonWrapper.TryWrapToolbarButton("Chatterer", "UI");
                chatterer_toolbar_button.TexturePath = "Chatterer/Textures/chatterer_icon_toolbar";
                chatterer_toolbar_button.ToolTip = "Open/Close Chatterer UI";
                chatterer_toolbar_button.SetButtonVisibility(GameScenes.FLIGHT);
                chatterer_toolbar_button.AddButtonClickHandler((e) =>
                {
                    if (debugging) Debug.Log("[CHATR] Toolbar UI button clicked, when hide_all_windows = " + hide_all_windows);

                    if (launcherButton == null && ToolbarButtonWrapper.ToolbarManagerPresent)
                    {
                        UIToggle();
                    }
                    else if (launcherButton != null)
                    {
                        if (hide_all_windows)
                        {
                            launcherButton.SetTrue();
                            if (debugging) Debug.Log("[CHATR] Blizzy78's Toolbar UI button clicked, launcherButton.State = " + launcherButton.State);
                        }
                        else if (!hide_all_windows)
                        {
                            launcherButton.SetFalse();
                            if (debugging) Debug.Log("[CHATR] Blizzy78's Toolbar UI button clicked, saving settings... & launcherButton.State = " + launcherButton.State);
                        }
                    }
                });
            }
        }

        private void OnGUIApplicationLauncherReady()
        {
            // Create the button in the KSP AppLauncher
            if (launcherButton == null && !useBlizzy78Toolbar)
            {
                if (debugging) Debug.Log("[CHATR] Building ApplicationLauncherButton");
                                
                launcherButton = ApplicationLauncher.Instance.AddModApplication(UIToggle, UIToggle,
                                                                            null, null,
                                                                            null, null,
                                                                            ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                                                                            chatterer_button_idle);
            }
        }

         private void launcherButtonTexture_check()
         {
            // launcherButton texture change check

             if (all_muted)
             {
                 if (initial_chatter.isPlaying) SetAppLauncherButtonTexture(chatterer_button_TX_muted);
                 else if (response_chatter.isPlaying) SetAppLauncherButtonTexture(chatterer_button_RX_muted);
                 else if (sstv.isPlaying) SetAppLauncherButtonTexture(chatterer_button_SSTV_muted);
                 else SetAppLauncherButtonTexture(chatterer_button_idle_muted);
             }
             else
             {
                 if (initial_chatter.isPlaying) SetAppLauncherButtonTexture(chatterer_button_TX);
                 else if (response_chatter.isPlaying) SetAppLauncherButtonTexture(chatterer_button_RX);
                 else if (sstv.isPlaying) SetAppLauncherButtonTexture(chatterer_button_SSTV);
                 else SetAppLauncherButtonTexture(chatterer_button_idle);
             }

             //if (inRadioContact) // for later use when RT2 support is implemented
             //{
             //
             //}
             //else SetAppLauncherButtonTexture(chatterer_button_disabled);
         }

        private void SetAppLauncherButtonTexture(Texture2D tex2d)
        {
            // Set new launcherButton texture
            
            if (launcherButton != null)
            {
                if (tex2d != chatterer_button_Texture)
                {
                    chatterer_button_Texture = tex2d;
                    launcherButton.SetTexture(tex2d);

                    if (debugging) Debug.Log("[CHATR] SetAppLauncherButtonTexture(" + tex2d + ");");
                }
            }
        }

        public void UIToggle()
        {
            if (!hide_all_windows)
            {
                hide_all_windows = true;
                save_plugin_settings();

                if (debugging) Debug.Log("[CHATR] UIToggle(OFF)");
            }
            else
            {
                hide_all_windows = !hide_all_windows;

                if (debugging) Debug.Log("[CHATR] UIToggle(ON)");
            }
        }

        public void launcherButtonRemove()
        {
            if (launcherButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(launcherButton);

                if (debugging) Debug.Log("[CHATR] launcherButtonRemove");
            }

            else if (debugging) Debug.Log("[CHATR] launcherButtonRemove (useless attempt)");
        }

        public void OnSceneChangeRequest(GameScenes _scene)
        {
            launcherButtonRemove();
        }

        internal void OnDestroy() 
        {
            if (debugging) Debug.Log("[CHATR] OnDestroy() START");

            // Remove the button from the Blizzy's toolbar
            if (chatterer_toolbar_button != null)
            {
                chatterer_toolbar_button.Destroy();

                if (debugging) Debug.Log("[CHATR] OnDestroy() Blizzy78's toolbar button removed");
            }
            // Un-register the callbacks
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIApplicationLauncherReady);
            GameEvents.onGameSceneLoadRequested.Remove(OnSceneChangeRequest);
            
            // Remove the button from the KSP AppLauncher
            launcherButtonRemove();

            if (debugging) Debug.Log("[CHATR] OnDestroy() END");
        }

        private void start_GUI()
        {
            if (debugging) Debug.Log("[CHATR] start_GUI()");
            
            RenderingManager.AddToPostDrawQueue(3, new Callback(draw_GUI));	//start the GUI
            gui_running = true;
        }

        private void stop_GUI()
        {
            if (debugging) Debug.Log("[CHATR] stop_GUI()");
            
            RenderingManager.RemoveFromPostDrawQueue(3, new Callback(draw_GUI)); //stop the GUI
            gui_running = false;
        }

        private void set_gui_styles()
        {
            label_txt_left = new GUIStyle(GUI.skin.label);
            //label_txt_left.normal.textColor = Color.white;
            label_txt_left.normal.textColor = Color.white;
            label_txt_left.alignment = TextAnchor.MiddleLeft;

            label_txt_center = new GUIStyle(GUI.skin.label);
            //label_txt_center.normal.textColor = Color.white;
            label_txt_center.normal.textColor = Color.white;
            label_txt_center.alignment = TextAnchor.MiddleCenter;

            label_txt_right = new GUIStyle(GUI.skin.label);
            label_txt_right.normal.textColor = Color.white;
            label_txt_right.alignment = TextAnchor.MiddleRight;

            label_txt_bold = new GUIStyle(GUI.skin.label);
            label_txt_bold.normal.textColor = Color.white;
            label_txt_bold.fontStyle = FontStyle.Bold;
            label_txt_bold.alignment = TextAnchor.MiddleLeft;

            label_txt_red_center = new GUIStyle(GUI.skin.label);
            label_txt_red_center.normal.textColor = Color.white;
            label_txt_red_center.alignment = TextAnchor.MiddleCenter;

            button_txt_left = new GUIStyle(GUI.skin.button);
            button_txt_left.normal.textColor = Color.white;
            button_txt_left.alignment = TextAnchor.MiddleLeft;

            button_txt_right = new GUIStyle(GUI.skin.button);
            button_txt_right.normal.textColor = Color.white;
            button_txt_right.alignment = TextAnchor.MiddleRight;

            button_txt_center = new GUIStyle(GUI.skin.button);
            button_txt_center.normal.textColor = Color.white;
            button_txt_center.alignment = TextAnchor.MiddleCenter;

            //button_txt_center_green = new GUIStyle(GUI.skin.button);
            //button_txt_center_green.normal.textColor = button_txt_center_green.hover.textColor = button_txt_center_green.active.textColor = button_txt_center_green.focused.textColor = Color.green;
            //button_txt_center_green.alignment = TextAnchor.MiddleCenter;

            gs_tooltip = new GUIStyle(GUI.skin.box);
            gs_tooltip.normal.background = GUI.skin.window.normal.background;
            gs_tooltip.normal.textColor = XKCDColors.LightGrey;
            gs_tooltip.fontSize = 9;

            button_txt_left_bold = new GUIStyle(GUI.skin.button);
            button_txt_left_bold.normal.textColor = Color.white;
            button_txt_left_bold.fontStyle = FontStyle.Bold;
            button_txt_left_bold.alignment = TextAnchor.MiddleLeft;

            //xkcd_label = new GUIStyle(GUI.skin.label);
            //xkcd_label.normal.textColor = Color.white;
            //xkcd_label.alignment = TextAnchor.MiddleLeft;


            //reset_menu_gs();
            //if (active_menu == "sliders") gs_menu_sliders = button_txt_center_green;
            //if (active_menu == "audiosets") gs_menu_audiosets = button_txt_center_green;
            //if (active_menu == "remotetech") gs_menu_remotetech = button_txt_center_green;
            //if (active_menu == "settings") gs_menu_settings = button_txt_center_green;

            //reset_beep_gs();
            //gs_beep1 = button_txt_center_green;

            gui_styles_set = true;

            if (debugging) Debug.Log("[CHATR] GUI styles set");
        }

        private void build_skin_list()
        {
            // GUISkin[] skin_array = AssetBase.FindObjectsOfTypeIncludingAssets(typeof(GUISkin)) as GUISkin[]; [Obsolete("use Resources.FindObjectsOfTypeAll instead.")]
            GUISkin[] skin_array = Resources.FindObjectsOfTypeAll(typeof(GUISkin)) as GUISkin[];
            g_skin_list = new List<GUISkin>();

            foreach (GUISkin _skin in skin_array)
            {
                // Some skins just don't look good here so skip them
                if (_skin.name != "PlaqueDialogSkin"
                    && _skin.name != "FlagBrowserSkin"
                    && _skin.name != "SSUITextAreaDefault"
                    && _skin.name != "ExperimentsDialogSkin"
                    && _skin.name != "ExpRecoveryDialogSkin"
                    && _skin.name != "PartTooltipSkin"
                    // Third party known skin mess up
                    && _skin.name != "UnityWKSPButtons"
                    && _skin.name != "Unity"
                    && _skin.name != "Default"
                    // Dupes
                    && _skin.name != "GameSkin"
                    && _skin.name != "GameSkin(Clone)"
                    && _skin.name != "KSP window 4"
                    && _skin.name != "KSP window 6"
                    && _skin.name != "KSP window 7"
                   )
                {
                    // Build wanted skin only list
                    g_skin_list.Add(_skin);
                }
            }

            if (debugging) Debug.Log("[CHATR] skin list built, count = " + g_skin_list.Count);
        }

        protected void draw_GUI()
        {
            //Apply a skin
            if (skin_index == 0) GUI.skin = null;
            else GUI.skin = g_skin_list[skin_index - 1];

            if (gui_styles_set == false) set_gui_styles();  //run this once to set a few GUIStyles

            int window_id = window_base_id;

            //main window
            if (hide_all_windows == false) main_window_pos = GUILayout.Window(window_id, main_window_pos, main_gui, main_window_title + this_version, GUILayout.Height(10f), GUILayout.Width(280f));

            //probe sample selector
            probe_sample_selector_window_id = ++window_id;
            if (hide_all_windows == false && show_probe_sample_selector) probe_sample_selector_window_pos = GUILayout.Window(probe_sample_selector_window_id, probe_sample_selector_window_pos, probe_sample_selector_gui, "Sample Selector", GUILayout.Height(350f), GUILayout.Width(280f));

            //Background sample
            AAE_background_sample_selector_window_id = ++window_id;
            if (hide_all_windows == false && show_AAE_background_sample_selector) AAE_background_sample_selector_window_pos = GUILayout.Window(AAE_background_sample_selector_window_id, AAE_background_sample_selector_window_pos, AAE_background_sample_selector_gui, "Background Sample Selector", GUILayout.Height(350f), GUILayout.Width(280f));

            //lab window
            lab_window_id = ++window_id;
            if (hide_all_windows == false && show_lab_gui) lab_window_pos = GUILayout.Window(lab_window_id, lab_window_pos, testing_gui, "The Lab", GUILayout.Height(10f), GUILayout.Width(300f));

            //chatter filters
            chatter_filter_settings_window_id = ++window_id;
            if (hide_all_windows == false && show_chatter_filter_settings)
            {
                chatter_filter_settings_window_pos = GUILayout.Window(chatter_filter_settings_window_id, chatter_filter_settings_window_pos, chatter_filter_settings_gui, "Chatter Filters", GUILayout.Height(10f), GUILayout.Width(280f));
            }

            //beep filters
            foreach (BeepSource source in beepsource_list)
            {
                source.settings_window_id = ++window_id;
                if (hide_all_windows == false && source.show_settings_window) source.settings_window_pos = GUILayout.Window(source.settings_window_id, source.settings_window_pos, beep_filter_settings_gui, "Beep " + source.beep_name + " Filters", GUILayout.Height(10f), GUILayout.Width(280f));
            }
        }

        private void main_gui(int window_id)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();

            //Show "Chatter" menu button
            if (chatter_exists && vessel.GetCrewCount() > 0)
            {
                if (GUILayout.Button("Chatter"))
                {
                    menu = "chatter";
                }
            }

            //Show "Beeps" button
            if (beeps_exists || sstv_exists)
            {
                if (GUILayout.Button("Beeps"))
                {
                    menu = "beeps";
                }
            }

            //Show "AAE" button
            if (aae_backgrounds_exist || aae_soundscapes_exist || aae_breathing_exist || aae_airlock_exist)
            {
                if (GUILayout.Button("AAE"))
                {
                    menu = "AAE";
                }
            }

            //Show "Settings"
            if (GUILayout.Button("Settings")) menu = "settings";

            //Mute button
            string muted = "Mute";
            if (mute_all) muted = "Muted";

            if (GUILayout.Button(muted, GUILayout.ExpandWidth(false)))
            {
                mute_all = !mute_all;
                //if (mute_all == false) SetAppLauncherButtonTexture(chatterer_button_idle);
                //else SetAppLauncherButtonTexture(chatterer_button_idle_muted);

                if (debugging) Debug.Log("[CHATR] Mute = " + mute_all);
            }

            string closeUI = "Close";
            if (GUILayout.Button(closeUI, GUILayout.ExpandWidth(false)))
            {
                if (launcherButton == null && ToolbarButtonWrapper.ToolbarManagerPresent)
                {
                    UIToggle();
                }
                else if (launcherButton != null)
                {
                    launcherButton.SetFalse();
                }
            }
            
            GUILayout.EndHorizontal();

            //Separator
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            GUILayout.Label(line_512x4, GUILayout.ExpandWidth(false), GUILayout.Width(275f), GUILayout.Height(10f));
            GUILayout.EndHorizontal();

            //Display GUI accordingly
            if (menu == "chatter" && vessel.GetCrewCount() > 0) chatter_gui();
            else if (menu == "beeps") beeps_gui();
            else if (menu == "AAE") AAE_gui();
            else if (menu == "settings") settings_gui();
            else beeps_gui();

            ////new version info (if any)
            //if (recvd_latest_version && latest_version != "")
            //{
            //    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            //    GUILayout.Label(latest_version, label_txt_left);
            //    GUILayout.EndHorizontal();
            //}

            //Tooltips
            if (show_tooltips && GUI.tooltip != "") tooltips(main_window_pos);

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void chatter_gui()
        {
            GUIContent _content = new GUIContent();

            //Chatter frequency
            chatter_freq = Convert.ToInt32(Math.Round(chatter_freq_slider));
            string chatter_freq_str = "";
            if (chatter_freq == 0) chatter_freq_str = "No chatter";
            else
            {
                if (chatter_freq == 1) chatter_freq_str = "180-300s";
                else if (chatter_freq == 2) chatter_freq_str = "90-180s";
                else if (chatter_freq == 3) chatter_freq_str = "60-90s";
                else if (chatter_freq == 4) chatter_freq_str = "30-60s";
                else if (chatter_freq == 5) chatter_freq_str = "10-30s";
            }

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            _content.text = "Chatter frequency: " + chatter_freq_str;
            _content.tooltip = "How often chatter will play";
            GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
            chatter_freq_slider = GUILayout.HorizontalSlider(chatter_freq_slider, 0, 5f, GUILayout.Width(100f));
            GUILayout.EndHorizontal();

            if (chatter_freq != prev_chatter_freq)
            {
                if (debugging) Debug.Log("[CHATR] chatter_freq has changed, setting new delay between exchanges...");
                if (chatter_freq == 0)
                {
                    exchange_playing = false;
                    secs_since_initial_chatter = 0;
                }
                secs_since_last_exchange = 0;
                set_new_delay_between_exchanges();
                prev_chatter_freq = chatter_freq;
            }

            //Chatter volume
            _content.text = "Chatter volume: " + (chatter_vol_slider * 100).ToString("F0") + "%";
            _content.tooltip = "Volume of chatter audio";
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
            chatter_vol_slider = GUILayout.HorizontalSlider(chatter_vol_slider, 0, 1f, GUILayout.Width(130f));
            GUILayout.EndHorizontal();

            if (chatter_vol_slider != prev_chatter_vol_slider)
            {
                if (debugging) Debug.Log("[CHATR] Changing chatter AudioSource volume...");
                initial_chatter.volume = chatter_vol_slider;
                response_chatter.volume = chatter_vol_slider;
                prev_chatter_vol_slider = chatter_vol_slider;
            }

            //Quindar
            _content.text = "Quindar volume: " + (quindar_vol_slider * 100).ToString("F0") + "%";
            _content.tooltip = "Volume of beeps before and after chatter";
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
            quindar_vol_slider = GUILayout.HorizontalSlider(quindar_vol_slider, 0, 1f, GUILayout.Width(130f));
            GUILayout.EndHorizontal();

            if (quindar_vol_slider != prev_quindar_vol_slider)
            {
                if (debugging) Debug.Log("[CHATR] Quindar volume has been changed...");
                quindar1.volume = quindar_vol_slider;
                quindar2.volume = quindar_vol_slider;
                prev_quindar_vol_slider = quindar_vol_slider;
            }

            if (show_advanced_options)
            {
                //Chatter sets
                _content.text = "ChatterSets";
                _content.tooltip = "Show currently loaded chatter audio";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) show_chatter_sets = !show_chatter_sets;
                GUILayout.Label("", GUILayout.ExpandWidth(true));    //spacer
                _content.text = "Filters";
                _content.tooltip = "Adjust filters for chatter audio";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    show_chatter_filter_settings = !show_chatter_filter_settings;
                }
                GUILayout.EndHorizontal();

                if (show_chatter_sets)
                {
                    int i;
                    for (i = 0; i < chatter_array.Count; i++)
                    {
                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                        bool temp = chatter_array[i].is_active;
                        _content.text = chatter_array[i].directory + " (" + (chatter_array[i].capcom.Count + chatter_array[i].capsule.Count).ToString() + " clips)";
                        _content.tooltip = "Toggle this chatter set on/off";
                        chatter_array[i].is_active = GUILayout.Toggle(chatter_array[i].is_active, _content, GUILayout.ExpandWidth(true));
                        _content.text = "Remove";
                        _content.tooltip = "Remove this chatter set from the list";
                        if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                        {
                            //Remove this set
                            chatter_array.RemoveAt(i);
                            load_chatter_audio();
                            break;
                        }

                        if (temp != chatter_array[i].is_active) load_toggled_chatter_sets();    //reload toggled audio clips if any set is toggled on/off

                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                    custom_dir_name = GUILayout.TextField(custom_dir_name, GUILayout.Width(150f));
                    GUILayout.Label("", GUILayout.ExpandWidth(true));   //spacer
                    _content.text = "Load";
                    _content.tooltip = "Try to load chatter set with this name";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        bool already_loaded = false;
                        foreach (ChatterAudioList r in chatter_array)
                        {
                            //check if this set is already loaded
                            if (custom_dir_name == r.directory) already_loaded = true;
                        }

                        if (custom_dir_name.Trim() != "" && custom_dir_name != "directory name" && already_loaded == false)
                        {
                            //set name isn't blank, "directory name", or already loaded.  load it.
                            chatter_array.Add(new ChatterAudioList());
                            chatter_array[chatter_array.Count - 1].directory = custom_dir_name.Trim();
                            chatter_array[chatter_array.Count - 1].is_active = true;

                            //reset custom_dir_name
                            custom_dir_name = "directory name";
                            //reload audio
                            load_chatter_audio();
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        private void beeps_gui()
        {
            GUIContent _content = new GUIContent();

            //Beeps
            if (beeps_exists)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                if (show_advanced_options)
                {
                    //Decrease beepsources
                    _content.text = "Rmv";
                    _content.tooltip = "Remove the last beepsource";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        if (beepsource_list.Count > 1)
                        {
                            //remove a beepsource
                            if (debugging) Debug.Log("[CHATR] num_sources = " + beepsource_list.Count);

                            Destroy(beepsource_list[beepsource_list.Count - 1].beep_player);   //destroy GameObject holding Source and Filters
                            if (debugging) Debug.Log("[CHATR] beep_player destroyed");

                            if (debugging) Debug.Log("[CHATR] attempting to remove BeepSource at index " + (beepsource_list.Count - 1));

                            beepsource_list.RemoveAt(beepsource_list.Count - 1);  //remove the last BeepSource from the list

                            if (debugging) Debug.Log("[CHATR] BeepSource at index " + (beepsource_list.Count) + " removed from beepsource_list");



                            //line below is a problem
                            // sel_beep_src can only be 0-9
                            //set = 0 whenever it is lowered until a more elegant solution can be found
                            //RBRBeepSource bm = beepsource_list[(((sel_beep_page - 1) * 10) + sel_beep_src)];


                            //if (sel_beep_src == beepsource_list.Count) sel_beep_src = beepsource_list.Count - 1;    //if selected source was just removed, set it to highest available
                            sel_beep_src = 0;

                            //beepsources have decreased, check if sel_page index is out of range
                            num_beep_pages = beepsource_list.Count / 10;
                            if (beepsource_list.Count % 10 != 0) num_beep_pages++;

                            if (num_beep_pages != prev_num_pages)
                            {

                                //last page is no longer needed in the grid
                                //set sel_page to the new last page if it is out of range
                                if (sel_beep_page > num_beep_pages) sel_beep_page = num_beep_pages;
                                //set sel_source to 0
                                sel_beep_src = 0;
                                prev_num_pages = num_beep_pages;
                            }
                        }
                    }
                    //if (debugging) Debug.Log("[CHATR] - button OK");

                    if (num_beep_pages > 1)
                    {
                        _content.text = "◄";
                        _content.tooltip = "Previous page";
                        if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                        {
                            sel_beep_page--;

                            if (sel_beep_page < 1)
                            {
                                sel_beep_page = 1;
                                if (debugging) Debug.Log("[CHATR] this is the first page");
                            }
                            else
                            {
                                sel_beep_src = 0;
                                if (debugging) Debug.Log("[CHATR] page back");
                            }
                        }
                    }

                    //if (debugging) Debug.Log("[CHATR] ◄ button OK");
                }

                //Beep selection grid
                List<string> sources = new List<string>();
                foreach (BeepSource b in beepsource_list)
                {

                    //when sel_page = 1, want to add 1-10
                    //when sel_page = 2, want to add 11-20

                    //min = ((sel_page - 1) * 10) + 1
                    //max <= sel_page * 10

                    int beep_num = Int32.Parse(b.beep_name);

                    if (beep_num >= ((sel_beep_page - 1) * 10) + 1 && beep_num <= sel_beep_page * 10)
                    {
                        sources.Add(b.beep_name);
                    }
                }

                //GUIContent[] _content_array = sources.ToArray();
                //
                //GUIContent[] asset_list = { new GUIContent("Wallpaper", "Change Wallpaper"), new GUIContent("Floor", "Change Floor"), new GUIContent("Light", "Switch Light") };


                string[] s = sources.ToArray();
                int sel_grid_width = 5;
                if (sources.Count < 5) sel_grid_width = sources.Count;

                sel_beep_src = GUILayout.SelectionGrid(sel_beep_src, s, sel_grid_width, GUILayout.ExpandWidth(true));
                //if (debugging) Debug.Log("[CHATR] grid OK");

                if (show_advanced_options)
                {
                    //page next
                    if (num_beep_pages > 1)
                    {
                        _content.text = "►";
                        _content.tooltip = "Next page";
                        if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                        {
                            sel_beep_page++;

                            if (sel_beep_page > num_beep_pages)
                            {
                                sel_beep_page = num_beep_pages;
                                if (debugging) Debug.Log("[CHATR] this is the last page");
                            }
                            else
                            {
                                sel_beep_src = 0;
                                if (debugging) Debug.Log("[CHATR] page next");
                            }
                        }
                    }
                    //if (debugging) Debug.Log("[CHATR] ► button OK");

                    //Increase beepsources
                    _content.text = "Add";
                    _content.tooltip = "Add a new beepsource";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        add_new_beepsource();
                        if (debugging) Debug.Log("[CHATR] new BeepSource added");
                        save_plugin_settings();



                        //when adding a new source that will create a new page, change to that page and set sel_beep to 0


                        //beepsources have increased, check if a new page is needed
                        num_beep_pages = beepsource_list.Count / 10;
                        if (beepsource_list.Count % 10 != 0) num_beep_pages++;

                        if (num_beep_pages != prev_num_pages)
                        {

                            //a new page is needed in the grid
                            //set sel_page to the new page
                            //sel_beep_page = num_beep_pages;
                            //set sel_source to 0
                            //sel_beep_src = 0;
                            prev_num_pages = num_beep_pages;
                        }
                    }
                    //if (debugging) Debug.Log("[CHATR] + button OK");
                }
                GUILayout.EndHorizontal();

                //if (debugging) Debug.Log("[CHATR] beepsource_list.Count = " + beepsource_list.Count);
                //if (debugging) Debug.Log("[CHATR] num_beep_pages = " + num_beep_pages);
                //if (debugging) Debug.Log("[CHATR] sel_beep_page = " + sel_beep_page);
                //if (debugging) Debug.Log("[CHATR] sel_beep_src = " + sel_beep_src);
                //if (debugging) Debug.Log("[CHATR] beepsource_list index [((sel_beep_page - 1) * 10) + sel_beep_src] = " + (((sel_beep_page - 1) * 10) + sel_beep_src));

                BeepSource bm = beepsource_list[(((sel_beep_page - 1) * 10) + sel_beep_src)];   //shortcut   0-9 only, but correspond to the correct beepsource

                //if (debugging) Debug.Log("[CHATR] shortcut OK");



                if (bm.precise)
                {
                    //show exact slider
                    bm.precise_freq = Convert.ToInt32(Math.Round(bm.precise_freq_slider));
                    string beep_freq_str = "";
                    if (bm.precise_freq == -1) beep_freq_str = "No beeps";
                    else if (bm.precise_freq == 0) beep_freq_str = "Loop";
                    else beep_freq_str = "Every " + bm.precise_freq.ToString() + "s";

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    _content.text = "Beep frequency: " + beep_freq_str;
                    _content.tooltip = "How often this beepsource will play";
                    GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                    bm.precise_freq_slider = GUILayout.HorizontalSlider(bm.precise_freq_slider, -1f, 60f, GUILayout.Width(100f));
                    GUILayout.EndHorizontal();

                    if (bm.precise_freq != bm.prev_precise_freq)
                    {
                        if (debugging) Debug.Log("[CHATR] precise_freq has changed, resetting beep_timer...");
                        bm.timer = 0;
                        bm.prev_precise_freq = bm.precise_freq;
                        if (bm.precise_freq == 0 && bm.current_clip == "Random")
                        {
                            //frequency has changed to looped mode
                            //current clip == random
                            //not allowed, too silly
                            bm.current_clip = "Default";
                        }
                    }
                }
                else
                {
                    //show loose slider
                    bm.loose_freq = Convert.ToInt32(Math.Round(bm.loose_freq_slider));
                    string beep_freq_str = "";
                    if (bm.loose_freq == 0) beep_freq_str = "No beeps";
                    else
                    {
                        if (bm.loose_freq == 1) beep_freq_str = "120-300s";
                        else if (bm.loose_freq == 2) beep_freq_str = "60-120s";
                        else if (bm.loose_freq == 3) beep_freq_str = "30-60s";
                        else if (bm.loose_freq == 4) beep_freq_str = "15-30s";
                        else if (bm.loose_freq == 5) beep_freq_str = "5-15s";
                        else if (bm.loose_freq == 6) beep_freq_str = "1-5s";
                    }

                    _content.text = "Beep frequency: " + beep_freq_str;
                    _content.tooltip = "How often this beepsource will play";
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                    bm.loose_freq_slider = GUILayout.HorizontalSlider(bm.loose_freq_slider, 0, 6f, GUILayout.Width(100f));
                    GUILayout.EndHorizontal();

                    if (bm.loose_freq != bm.prev_loose_freq)
                    {
                        if (debugging) Debug.Log("[CHATR] loose_freq has changed, resetting beep_timer...");
                        new_beep_loose_timer_limit(bm);
                        bm.timer = 0;
                        bm.prev_loose_freq = bm.loose_freq;
                    }
                }

                //Volume
                _content.text = "Beep volume: " + (bm.audiosource.volume * 100).ToString("F0") + "%";
                _content.tooltip = "Volume of this beepsource";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                bm.audiosource.volume = GUILayout.HorizontalSlider(bm.audiosource.volume, 0, 1f, GUILayout.Width(130f));
                GUILayout.EndHorizontal();

                //Pitch
                _content.text = "Beep pitch: " + (bm.audiosource.pitch * 100).ToString("F0") + "%";
                _content.tooltip = "Pitch of this beepsource";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                bm.audiosource.pitch = GUILayout.HorizontalSlider(bm.audiosource.pitch, 0.1f, 5f, GUILayout.Width(130f));
                GUILayout.EndHorizontal();

                //Beep timing
                string beep_timing_str = "Loose";
                if (bm.precise) beep_timing_str = "Precise";


                _content.text = beep_timing_str;
                _content.tooltip = "Switch between timing modes";
                GUILayout.BeginHorizontal();
                GUILayout.Label("Timing:");
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    //timing mode is being switched
                    bm.precise = !bm.precise;
                    bm.timer = 0;
                    bm.audiosource.loop = false;
                    bm.audiosource.Stop();

                    if (bm.precise)
                    {
                        if (debugging) Debug.Log("[CHATR] beep timing mode has changed to precise");
                        if (bm.current_clip == "Random" && bm.precise_freq == 0)
                        {
                            //disallow random looped clips
                            bm.current_clip = "Default";
                        }
                        //else
                        //{
                        //bm.audiosource.clip = all_beep_clips[bm.current_clip - 1];
                        //}
                        set_beep_clip(bm);
                    }
                    else new_beep_loose_timer_limit(bm);   //set new loose time limit
                }

                //Sample selector
                _content.text = bm.current_clip;
                _content.tooltip = "Click to change the current beep sample";
                GUILayout.Label("", GUILayout.ExpandWidth(true));    //spacer to align "Filters" to the right
                GUILayout.Label("Sample:", GUILayout.ExpandWidth(false));
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) show_probe_sample_selector = !show_probe_sample_selector;

                GUILayout.EndHorizontal();



                if (show_advanced_options)
                {
                    //Add copy/paste single beepsource
                    //Add copy all/paste all beepsources

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                    _content.text = "Copy";
                    _content.tooltip = "Copy beepsource to clipboard";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        //copy this beepsource values to beepsource_clipboard
                        copy_beepsource_values(bm);
                    }
                    if (beepsource_clipboard != null)
                    {
                        _content.text = "Paste";
                        _content.tooltip = "Paste beepsource from clipboard";
                        if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                        {
                            //paste beepsource_clipboard values to this beepsource
                            paste_beepsource_values(bm);
                        }
                    }

                    //Filters
                    GUILayout.Label("", GUILayout.ExpandWidth(true));    //spacer to align "Filters" to the right
                    _content.text = "Filters";
                    _content.tooltip = "Open filter settings window for this beepsource";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        bm.show_settings_window = !bm.show_settings_window;
                    }

                    GUILayout.EndHorizontal();
                }
            }

            //line to separate when both exist
            if (beeps_exists && sstv_exists)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
                GUILayout.Label(line_512x4, GUILayout.ExpandWidth(false), GUILayout.Width(275f), GUILayout.Height(10f));
                GUILayout.EndHorizontal();
            }

            //SSTV
            if (sstv_exists)
            {
                sstv_freq = Convert.ToInt32(Math.Round(sstv_freq_slider));
                string sstv_freq_str = "";
                if (sstv_freq == 0) sstv_freq_str = "No SSTV";
                else
                {
                    if (sstv_freq == 1) sstv_freq_str = "1800-3600s";
                    else if (sstv_freq == 2) sstv_freq_str = "600-1800s";
                    else if (sstv_freq == 3) sstv_freq_str = "300-600s";
                    else if (sstv_freq == 4) sstv_freq_str = "120-300s";
                }

                _content.text = "SSTV frequency: " + sstv_freq_str;
                _content.tooltip = "How often SSTV will play";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                sstv_freq_slider = GUILayout.HorizontalSlider(sstv_freq_slider, 0, 4f, GUILayout.Width(100f));
                GUILayout.EndHorizontal();

                if (sstv_freq != prev_sstv_freq)
                {
                    if (debugging) Debug.Log("[CHATR] sstv_freq has changed, setting new sstv timer limit...");
                    if (sstv_freq == 0) sstv.Stop();
                    else new_sstv_loose_timer_limit();
                    sstv_timer = 0;
                    prev_sstv_freq = sstv_freq;
                }

                _content.text = "SSTV volume: " + (sstv_vol_slider * 100).ToString("F0") + "%";
                _content.tooltip = "Volume of SSTV source";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                sstv_vol_slider = GUILayout.HorizontalSlider(sstv_vol_slider, 0, 1f, GUILayout.Width(130f));
                GUILayout.EndHorizontal();

                if (sstv_vol_slider != prev_sstv_vol_slider)
                {
                    if (debugging) Debug.Log("[CHATR] Changing SSTV AudioSource volume...");
                    sstv.volume = sstv_vol_slider;
                    prev_sstv_vol_slider = sstv_vol_slider;
                }
            }
        }

        private void AAE_gui()
        {
            GUIContent _content = new GUIContent();
            string truncated;   //truncate file names because some are stupid long

            if (aae_backgrounds_exist)
            {
                int i = 1;
                foreach (BackgroundSource src in backgroundsource_list)
                {
                    _content.text = "Background " + i + " volume: " + (src.audiosource.volume * 100).ToString("F0") + "%";
                    _content.tooltip = "Volume level for this Background audio";
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                    src.audiosource.volume = GUILayout.HorizontalSlider(src.audiosource.volume, 0, 1f, GUILayout.Width(100f));
                    GUILayout.EndHorizontal();

                    if (src.current_clip.Length > 30) truncated = src.current_clip.Substring(0, 27) + "...";
                    else truncated = src.current_clip;
                    _content.text = truncated;
                    _content.tooltip = "Click to change the selected sample";
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label("Sample:");
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        sel_background_src = src;
                        //show_sample_selector = !show_sample_selector;
                        show_AAE_background_sample_selector = !show_AAE_background_sample_selector;
                    }
                    GUILayout.EndHorizontal();
                    i++;
                }
            }

            //EVA breathing
            if (aae_breathing_exist)
            {
                _content.text = "Breath volume: " + (aae_breathing.volume * 100).ToString("F0") + "%";
                _content.tooltip = "Volume level for EVA breathing";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                aae_breathing.volume = GUILayout.HorizontalSlider(aae_breathing.volume, 0, 1f, GUILayout.Width(100f));
                GUILayout.EndHorizontal();
            }

            //Airlock
            if (aae_airlock_exist)
            {
                _content.text = "Airlock volume: " + (aae_airlock.volume * 100).ToString("F0") + "%";
                _content.tooltip = "Volume level for Airlock";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                aae_airlock.volume = GUILayout.HorizontalSlider(aae_airlock.volume, 0, 1f, GUILayout.Width(100f));
                GUILayout.EndHorizontal();
            }

            //Wind
            if (aae_wind_exist)
            {
                _content.text = "Wind volume: " + (aae_wind_vol_slider * 100).ToString("F0") + "%";
                _content.tooltip = "Volume level for surface wind";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                aae_wind_vol_slider = GUILayout.HorizontalSlider(aae_wind_vol_slider, 0, 1f, GUILayout.Width(100f));
                GUILayout.EndHorizontal();
            }

            //Soundscape
            if (aae_soundscapes_exist)
            {
                _content.text = "Soundscape volume: " + (aae_soundscape.volume * 100).ToString("F0") + "%";
                _content.tooltip = "Volume level for Soundscapes";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                aae_soundscape.volume = GUILayout.HorizontalSlider(aae_soundscape.volume, 0, 1f, GUILayout.Width(100f));
                GUILayout.EndHorizontal();

                aae_soundscape_freq = Convert.ToInt32(Math.Round(aae_soundscape_freq_slider));
                string soundscape_freq_str = "";
                if (aae_soundscape_freq == 0) soundscape_freq_str = "Disabled";
                else
                {
                    if (aae_soundscape_freq == 1) soundscape_freq_str = "5-10 min";
                    else if (aae_soundscape_freq == 2) soundscape_freq_str = "2-5 min";
                    else if (aae_soundscape_freq == 3) soundscape_freq_str = "1-2 min";
                    else if (aae_soundscape_freq == 4) soundscape_freq_str = "Continuous";
                }

                _content.text = "Soundscape frequency: " + soundscape_freq_str;
                _content.tooltip = "How often soundscapes will play";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                aae_soundscape_freq_slider = GUILayout.HorizontalSlider(aae_soundscape_freq_slider, 0, 4f, GUILayout.Width(60f));
                GUILayout.EndHorizontal();

                if (aae_soundscape_freq != aae_prev_soundscape_freq)
                {
                    if (aae_soundscape_freq == 0)
                    {
                        //soundscape turned off
                        aae_soundscape.Stop();
                    }
                    if (aae_soundscape_freq == 4)
                    {
                        //if freq = 4, continuous play of soundscape

                    }
                    else
                    {
                        if (debugging) Debug.Log("[CHATR] setting new soundscape1 timer limit...");
                        new_soundscape_loose_timer_limit();
                        aae_soundscape_timer = 0;
                    }

                    aae_prev_soundscape_freq = aae_soundscape_freq;
                }

                //Curently playing soundscape
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("Sample: ");
                if (aae_soundscape_current_clip.Length > 30) truncated = aae_soundscape_current_clip.Substring(0, 27) + "...";
                else truncated = aae_soundscape_current_clip;
                _content.text = truncated;
                _content.tooltip = "Click to skip to a new random soundscape";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    set_soundscape_clip();
                }
                GUILayout.EndHorizontal();
            }
        }

        private void settings_gui()
        {
            GUIContent _content = new GUIContent();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            _content.text = "Reset default settings";
            _content.tooltip = "Reset all chatterer settings to default";
            if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) load_plugin_defaults();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            _content.text = "Debug Mode";
            _content.tooltip = "Spam the log with more or less usefull reports";
            debugging = GUILayout.Toggle(debugging, _content);
            GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            //_content.text = "Allow update check";
            //_content.tooltip = "Allow plugin to check for a newer version via http";
            //http_update_check = GUILayout.Toggle(http_update_check, _content);
            //GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            _content.text = "Use per-vessel settings";
            _content.tooltip = "Every vessel will save/load its own individual settings";
            use_vessel_settings = GUILayout.Toggle(use_vessel_settings, _content);
            GUILayout.EndHorizontal();

            if (use_vessel_settings != prev_use_vessel_settings)
            {
                //setting has just changed
                if (use_vessel_settings)
                {
                    //just toggled on, load stuff
                    if (debugging) Debug.Log("[CHATR] Update() :: calling load_vessel_settings_node()");
                    load_vessel_settings_node(); //load and search for settings for this vessel
                    if (debugging) Debug.Log("[CHATR] Update() :: calling search_vessel_settings_node()");
                    search_vessel_settings_node();
                }
                prev_use_vessel_settings = use_vessel_settings;
            }

            if (ToolbarButtonWrapper.ToolbarManagerPresent)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                _content.text = "Use Blizzy78's toolbar only";
                _content.tooltip = "Hide stock Applaunch button";
                useBlizzy78Toolbar = GUILayout.Toggle(useBlizzy78Toolbar, _content);
                if (useBlizzy78Toolbar && launcherButton != null) launcherButtonRemove();
                if (!useBlizzy78Toolbar && launcherButton == null) OnGUIApplicationLauncherReady();
                GUILayout.EndHorizontal();
            }
            
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            _content.text = "Show tooltips";
            _content.tooltip = "It does something";
            show_tooltips = GUILayout.Toggle(show_tooltips, _content);
            GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            //_content.text = "Disable ElectricCharge usage";
            //_content.tooltip = "Plugin will not use any ElectricCharge";
            //disable_power_usage = GUILayout.Toggle(disable_power_usage, _content);
            //GUILayout.EndHorizontal();

            if (vessel.GetCrewCount() > 0)
            {
                _content.text = "Disable beeps during chatter";
                _content.tooltip = "Stop beeps from beeping while chatter is playing";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                disable_beeps_during_chatter = GUILayout.Toggle(disable_beeps_during_chatter, _content);
                GUILayout.EndHorizontal();

                //_content.text = "Enable RemoteTech integration";
                //_content.tooltip = "Capcom chatter is delayed/missed if not connected to a network";
                //GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                //remotetech_toggle = GUILayout.Toggle(remotetech_toggle, _content);
                //GUILayout.EndHorizontal();

                if (remotetech_toggle)
                {
                    GUIStyle txt_green = new GUIStyle(GUI.skin.label);
                    txt_green.normal.textColor = txt_green.focused.textColor = Color.green;
                    txt_green.alignment = TextAnchor.UpperLeft;
                    GUIStyle txt_red = new GUIStyle(GUI.skin.label);
                    txt_red.normal.textColor = txt_red.focused.textColor = Color.red;
                    txt_red.alignment = TextAnchor.UpperLeft;

                    string has_RT_SPU = "not found";
                    GUIStyle has_RT_text = txt_red;
                    if (hasRemoteTech)
                    {
                        has_RT_SPU = "found";
                        has_RT_text = txt_green;
                    }

                    string rt_connected = "Not connected to network";
                    GUIStyle RT_radio_contact_text = txt_red;
                    if (inRadioContact)
                    {
                        rt_connected = "Connected to network";
                        RT_radio_contact_text = txt_green;
                    }

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label("RemoteTech SPU " + has_RT_SPU, has_RT_text);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label(rt_connected, RT_radio_contact_text);
                    GUILayout.EndHorizontal();
                }

                //The Lab
                //GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                //show_lab_gui = GUILayout.Toggle(show_lab_gui, "The Lab");
                //GUILayout.EndHorizontal();
            }

            // Allowing "advanced options" even if crew < 0
            _content.text = "Show advanced options";
            _content.tooltip = "More chatter and beep options are displayed";
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            show_advanced_options = GUILayout.Toggle(show_advanced_options, _content);
            GUILayout.EndHorizontal();

            if (vessel.GetCrewCount() > 0)
            {
                //Insta-chatter key
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                if (set_insta_chatter_key == false)
                {
                    _content.text = "Insta-chatter key: " + insta_chatter_key.ToString();
                    _content.tooltip = "Press this key to play chatter now";
                    GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                    _content.text = "Change";
                    _content.tooltip = "Select a new insta-chatter key";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) set_insta_chatter_key = true;
                }
                GUILayout.EndHorizontal();

                if (set_insta_chatter_key)
                {
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label("Press new Insta-chatter key...", label_txt_left);
                    GUILayout.EndHorizontal();
                }

                if (set_insta_chatter_key && Event.current.isKey)
                {
                    insta_chatter_key = Event.current.keyCode;
                    set_insta_chatter_key = false;
                    insta_chatter_key_just_changed = true;
                }
            }

            //Insta-SSTV key
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            if (set_insta_sstv_key == false)
            {
                _content.text = "Insta-SSTV key: " + insta_sstv_key.ToString();
                _content.tooltip = "Press this key to play SSTV now";
                GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                _content.text = "Change";
                _content.tooltip = "Select a new insta-SSTV key";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) set_insta_sstv_key = true;
            }
            GUILayout.EndHorizontal();

            if (set_insta_sstv_key)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("Press new Insta-SSTV key...", label_txt_left);
                GUILayout.EndHorizontal();
            }

            if (set_insta_sstv_key && Event.current.isKey)
            {
                insta_sstv_key = Event.current.keyCode;
                set_insta_sstv_key = false;
                insta_sstv_key_just_changed = true;
            }

            //Skin picker
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            _content.text = "◄";
            _content.tooltip = "Select previous skin";
            if (GUILayout.Button(_content, GUILayout.ExpandWidth(true)))
            {
                skin_index--;
                if (skin_index < 0) skin_index = g_skin_list.Count;
                if (debugging) Debug.Log("[CHATR] new skin_index = " + skin_index + " :: g_skin_list.Count = " + g_skin_list.Count);
            }
            //if (debugging) Debug.Log("[CHATR] ◄ OK");

            string skin_name = "";
            if (skin_index == 0) skin_name = "None";
            else skin_name = g_skin_list[skin_index - 1].name;
            _content.text = skin_name;
            _content.tooltip = "Current skin";
            GUILayout.Label(_content, label_txt_center, GUILayout.ExpandWidth(true));
            //if (debugging) Debug.Log("[CHATR] skin label OK :: skin_list.Count = " + skin_list.Count);
            _content.text = "►";
            _content.tooltip = "Select next skin";
            if (GUILayout.Button(_content, GUILayout.ExpandWidth(true)))
            {
                skin_index++;
                if (skin_index > g_skin_list.Count) skin_index = 0;
                if (debugging) Debug.Log("[CHATR] new skin_index = " + skin_index + " :: g_skin_list.Count = " + g_skin_list.Count);
            }
            //if (debugging) Debug.Log("[CHATR] ► OK");

            GUILayout.EndHorizontal();
        }

        private void testing_gui(int window_id)
        {
            //EventData<Game> foo = GameEvents.onGameStateSaved;

            //if (foo == null) GUILayout.Label("[CHATR] EventData<Game> foo == null");
            //else GUILayout.Label("[CHATR] foo = " + foo.ToString());





            GUILayout.BeginVertical();

            GUILayout.Label("vessel.id.ToString() = " + vessel.id.ToString());

            GUILayout.Label("Application.platform = " + Application.platform);

            GUILayout.Label("Application.persistentDataPath = " + Application.persistentDataPath);

            GUILayout.Label("Application.unityVersion = " + Application.unityVersion);

            GUILayout.Label("Application.targetFrameRate = " + Application.targetFrameRate);

            //GUILayout.Label("AppDomain.CurrentDomain.DynamicDirectory = " + AppDomain.CurrentDomain.DynamicDirectory);

            //GUILayout.Label("AppDomain.CurrentDomain.RelativeSearchPath = " + AppDomain.CurrentDomain.RelativeSearchPath);

            GUILayout.Label("Application.absoluteURL = " + Application.absoluteURL);

            GUILayout.Label("Application.dataPath = " + Application.dataPath);

            //GUILayout.Label("Application.genuine = " + Application.genuine);

            //GUILayout.Label("Application.HasProLicense = " + Application.HasProLicense());

            GUILayout.Label("Application.internetReachability = " + Application.internetReachability);


            GUILayout.Label("str yep_yep: " + yep_yep);

            //Path.GetFileName
            //Path.GetFullPath();




            //allColors = KnownColor.DarkKhaki;


            //AudioReverbPreset arp = new AudioReverbPreset();
            //chatter_reverb_filter.reverbPreset = AudioReverbPreset.Alley;

            //AudioReverbPreset[] preset_list = Enum.GetValues(typeof(AudioReverbPreset)) as AudioReverbPreset[];

            //foreach (var val in preset_list)
            //{
            //    if (debugging) Debug.Log("[CHATR] preset val.ToString() = " +  val.ToString());
            //}

            //AssetBase ab = new AssetBase();

            //GUISkin[] jops = AssetBase.FindObjectsOfTypeIncludingAssets(typeof(GUISkin)) as GUISkin[];

            //foreach (GUISkin skin in jops)
            //{
            //    //if (debugging) Debug.Log("[CHATR] skin.name = " + skin.name);
            //    GUILayout.Label("skin.name = " + skin.name, xkcd_label);
            //}

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void chatter_filter_settings_gui(int window_id)
        {
            GUILayout.BeginVertical();

            string[] filters = { "Chorus", "Dist", "Echo", "HiPass", "LoPass", "Reverb" };

            chatter_sel_filter = GUILayout.SelectionGrid(chatter_sel_filter, filters, 3, GUILayout.ExpandWidth(true));

            chatter_reverb_preset_index = combined_filter_settings_gui(chatter_sel_filter, chatter_chorus_filter, chatter_distortion_filter, chatter_echo_filter, chatter_highpass_filter, chatter_lowpass_filter, chatter_reverb_filter, chatter_reverb_preset_index);

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            GUIContent _content = new GUIContent();
            _content.text = "Copy all";
            _content.tooltip = "Copy all filter values to clipboard";
            if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
            {
                copy_all_chatter_filters();
            }
            if (filters_clipboard != null)
            {
                _content.text = "Paste all";
                _content.tooltip = "Paste all filter values from clipboard";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    paste_all_chatter_filters();
                }
            }
            GUILayout.Label(" ", GUILayout.ExpandWidth(true));  //spacer

            if (GUILayout.Button("Close", GUILayout.ExpandWidth(false))) show_chatter_filter_settings = false;
            GUILayout.EndHorizontal();

            if (show_tooltips && GUI.tooltip != "") tooltips(chatter_filter_settings_window_pos);
            //{
            //    float w = 5.5f * GUI.tooltip.Length;
            //    float x = (Event.current.mousePosition.x < chatter_filter_settings_window_pos.width / 2) ? Event.current.mousePosition.x + 10 : Event.current.mousePosition.x - 10 - w;
            //    GUI.Box(new Rect(x, Event.current.mousePosition.y, w, 25f), GUI.tooltip, gs_tooltip);
            //}

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void beep_filter_settings_gui(int window_id)
        {
            GUILayout.BeginVertical();

            BeepSource source = null;

            foreach (BeepSource bm in beepsource_list)
            {
                if (bm.settings_window_id == window_id)
                {
                    source = bm;
                    break;
                }
            }

            if (source != null)
            {

                string[] filters = { "Chorus", "Dist", "Echo", "HiPass", "LoPass", "Reverb" };

                source.sel_filter = GUILayout.SelectionGrid(source.sel_filter, filters, 3, GUILayout.ExpandWidth(true));

                source.reverb_preset_index = combined_filter_settings_gui(source.sel_filter, source.chorus_filter, source.distortion_filter, source.echo_filter, source.highpass_filter, source.lowpass_filter, source.reverb_filter, source.reverb_preset_index);

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                GUIContent _content = new GUIContent();
                _content.text = "Copy all";
                _content.tooltip = "Copy all filter values to clipboard";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    copy_all_beep_filters(source);
                }
                if (filters_clipboard != null)
                {
                    _content.text = "Paste all";
                    _content.tooltip = "Paste all filter values from clipboard";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        paste_all_beep_filters(source);
                    }
                }
                GUILayout.Label(" ", GUILayout.ExpandWidth(true));  //spacer

                if (GUILayout.Button("Close", GUILayout.ExpandWidth(false))) source.show_settings_window = false;
                GUILayout.EndHorizontal();

                if (show_tooltips && GUI.tooltip != "") tooltips(source.settings_window_pos);
                //{
                //    float w = 5.5f * GUI.tooltip.Length;
                //    float x = (Event.current.mousePosition.x < source.settings_window_pos.width / 2) ? Event.current.mousePosition.x + 10 : Event.current.mousePosition.x - 10 - w;
                //    GUI.Box(new Rect(x, Event.current.mousePosition.y, w, 25f), GUI.tooltip, gs_tooltip);
                //}


                GUILayout.EndVertical();
                GUI.DragWindow();
            }
        }

        private int combined_filter_settings_gui(int sel_filter, AudioChorusFilter acf, AudioDistortionFilter adf, AudioEchoFilter aef, AudioHighPassFilter ahpf, AudioLowPassFilter alpf, AudioReverbFilter arf, int reverb_preset_index)
        {
            //chatter and beep settings window guis both call this function

            GUIContent _content = new GUIContent();

            if (sel_filter == 0)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                _content.text = "Enable";
                _content.tooltip = "Turn chorus filter on/off";
                acf.enabled = GUILayout.Toggle(acf.enabled, _content, GUILayout.ExpandWidth(true));
                _content.text = "Default";
                _content.tooltip = "Reset chorus filter to default";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) reset_chorus_filter(acf);
                GUILayout.EndHorizontal();

                _content.text = "Dry mix: " + (acf.dryMix * 100).ToString("F0") + "%";
                _content.tooltip = "Volume of original signal to pass to output";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                acf.dryMix = GUILayout.HorizontalSlider(acf.dryMix, 0, 1f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Wet mix 1: " + (acf.wetMix1 * 100).ToString("F0") + "%";
                _content.tooltip = "Volume of 1st chorus tap";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                acf.wetMix1 = GUILayout.HorizontalSlider(acf.wetMix1, 0, 1f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Wet mix 2: " + (acf.wetMix2 * 100).ToString("F0") + "%";
                _content.tooltip = "Volume of 2nd chorus tap";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                acf.wetMix2 = GUILayout.HorizontalSlider(acf.wetMix2, 0, 1f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Wet mix 3: " + (acf.wetMix3 * 100).ToString("F0") + "%";
                _content.tooltip = "Volume of 3rd chorus tap";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                acf.wetMix3 = GUILayout.HorizontalSlider(acf.wetMix3, 0, 1f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Delay: " + acf.delay.ToString("F2") + " ms";
                _content.tooltip = "Chorus delay in ms";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                acf.delay = GUILayout.HorizontalSlider(acf.delay, 0.1f, 100f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Rate: " + acf.rate.ToString("F2") + " Hz";
                _content.tooltip = "Chorus modulation rate in Hz";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                acf.rate = GUILayout.HorizontalSlider(acf.rate, 0, 20f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Depth: " + (acf.depth * 100).ToString("F0") + "%";
                _content.tooltip = "Chorus modulation depth";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                acf.depth = GUILayout.HorizontalSlider(acf.depth, 0, 1f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();
                
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                _content.text = "Copy Chorus";
                _content.tooltip = "Copy chorus values to clipboard";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    chorus_clipboard = new ConfigNode();
                    chorus_clipboard.AddValue("dry_mix", acf.dryMix);
                    chorus_clipboard.AddValue("wet_mix_1", acf.wetMix1);
                    chorus_clipboard.AddValue("wet_mix_2", acf.wetMix2);
                    chorus_clipboard.AddValue("wet_mix_3", acf.wetMix3);
                    chorus_clipboard.AddValue("delay", acf.delay);
                    chorus_clipboard.AddValue("rate", acf.rate);
                    chorus_clipboard.AddValue("depth", acf.depth);
                    
                    if (debugging) Debug.Log("[CHATR] chorus filter values copied to chorus clipboard");
                }
                if (chorus_clipboard != null)
                {
                    _content.text = "Load Chorus";
                    _content.tooltip = "Load chorus values from clipboard";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        acf.dryMix = Single.Parse(chorus_clipboard.GetValue("dry_mix"));
                        acf.wetMix1 = Single.Parse(chorus_clipboard.GetValue("wet_mix_1"));
                        acf.wetMix2 = Single.Parse(chorus_clipboard.GetValue("wet_mix_2"));
                        acf.wetMix3 = Single.Parse(chorus_clipboard.GetValue("wet_mix_3"));
                        acf.delay = Single.Parse(chorus_clipboard.GetValue("delay"));
                        acf.rate = Single.Parse(chorus_clipboard.GetValue("rate"));
                        acf.depth = Single.Parse(chorus_clipboard.GetValue("depth"));
                        
                        if (debugging) Debug.Log("[CHATR] chorus filter values loaded from chorus clipboard");
                    }
                }
                GUILayout.EndHorizontal();
            }
            else if (sel_filter == 1)
            {
                //Distortion
                _content.text = "Enable";
                _content.tooltip = "Turn distortion filter on/off";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                adf.enabled = GUILayout.Toggle(adf.enabled, _content, GUILayout.ExpandWidth(true));
                _content.text = "Default";
                _content.tooltip = "Reset distortion filter to default";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) reset_distortion_filter(adf);
                GUILayout.EndHorizontal();

                _content.text = "Distortion level: " + (adf.distortionLevel * 100).ToString("F0") + "%";
                _content.tooltip = "Distortion value";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                adf.distortionLevel = GUILayout.HorizontalSlider(adf.distortionLevel, 0, 1f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                _content.text = "Copy Dist";
                _content.tooltip = "Copy distortion values to clipboard";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    dist_clipboard = new ConfigNode();
                    dist_clipboard.AddValue("distortion_level", adf.distortionLevel);
                    if (debugging) Debug.Log("[CHATR] distortion filter values copied to distortion clipboard");
                }
                if (dist_clipboard != null)
                {
                    _content.text = "Load Dist";
                    _content.tooltip = "Load distortion values from clipboard";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        adf.distortionLevel = Single.Parse(dist_clipboard.GetValue("distortion_level"));
                        if (debugging) Debug.Log("[CHATR] distortion filter values loaded from distortion clipboard");
                    }
                }
                GUILayout.EndHorizontal();
            }
            else if (sel_filter == 2)
            {
                //Echo
                _content.text = "Enable";
                _content.tooltip = "Turn echo filter on/off";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                aef.enabled = GUILayout.Toggle(aef.enabled, _content, GUILayout.ExpandWidth(true));
                _content.text = "Default";
                _content.tooltip = "Reset echo filter to default";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) reset_echo_filter(aef);
                GUILayout.EndHorizontal();

                _content.text = "Delay: " + aef.delay.ToString("F0") + " ms";
                _content.tooltip = "Echo delay in ms";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                aef.delay = GUILayout.HorizontalSlider(aef.delay, 10f, 5000f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Decay ratio: " + aef.decayRatio.ToString("F2");
                _content.tooltip = "Echo decay per delay";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                aef.decayRatio = GUILayout.HorizontalSlider(aef.decayRatio, 0, 1f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Dry mix: " + (aef.dryMix * 100).ToString("F0") + "%";
                _content.tooltip = "Volume of original signal to pass to output";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                aef.dryMix = GUILayout.HorizontalSlider(aef.dryMix, 0, 1f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Wet mix: " + (aef.wetMix * 100).ToString("F0") + "%";
                _content.tooltip = "Volume of echo signal to pass to output";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                aef.wetMix = GUILayout.HorizontalSlider(aef.wetMix, 0, 1f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                _content.text = "Copy Echo";
                _content.tooltip = "Copy echo values to clipboard";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    echo_clipboard = new ConfigNode();
                    echo_clipboard.AddValue("delay", aef.delay);
                    echo_clipboard.AddValue("decay_ratio", aef.decayRatio);
                    echo_clipboard.AddValue("dry_mix", aef.dryMix);
                    echo_clipboard.AddValue("wet_mix", aef.wetMix);
                    if (debugging) Debug.Log("[CHATR] echo filter values copied to echo clipboard");
                }
                if (echo_clipboard != null)
                {
                    _content.text = "Load Echo";
                    _content.tooltip = "Load echo values from clipboard";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        aef.delay = Single.Parse(echo_clipboard.GetValue("delay"));
                        aef.decayRatio = Single.Parse(echo_clipboard.GetValue("decay_ratio"));
                        aef.dryMix = Single.Parse(echo_clipboard.GetValue("dry_mix"));
                        aef.wetMix = Single.Parse(echo_clipboard.GetValue("wet_mix"));
                        if (debugging) Debug.Log("[CHATR] echo filter values loaded from echo clipboard");
                    }
                }
                GUILayout.EndHorizontal();
            }
            else if (sel_filter == 3)
            {
                //Highpass
                _content.text = "Enable";
                _content.tooltip = "Turn highpass filter on/off";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                ahpf.enabled = GUILayout.Toggle(ahpf.enabled, _content, GUILayout.ExpandWidth(true));
                _content.text = "Default";
                _content.tooltip = "Reset highpass filter to default";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) reset_highpass_filter(ahpf);
                GUILayout.EndHorizontal();

                _content.text = "Cutoff freq: " + ahpf.cutoffFrequency.ToString("F2") + " Hz";
                _content.tooltip = "Highpass cutoff frequency in Hz";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                ahpf.cutoffFrequency = GUILayout.HorizontalSlider(ahpf.cutoffFrequency, 10f, 22000f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Resonance Q: " + ahpf.highpassResonaceQ.ToString("F2");
                _content.tooltip = "Highpass self-resonance dampening";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                ahpf.highpassResonaceQ = GUILayout.HorizontalSlider(ahpf.highpassResonaceQ, 1f, 10f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                _content.text = "Copy HiPass";
                _content.tooltip = "Copy highpass values to clipboard";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    hipass_clipboard = new ConfigNode();
                    hipass_clipboard.AddValue("cutoff_freq", ahpf.cutoffFrequency);
                    hipass_clipboard.AddValue("resonance_q", ahpf.highpassResonaceQ);
                    if (debugging) Debug.Log("[CHATR] highpass filter values copied to highpass clipboard");
                }
                if (hipass_clipboard != null)
                {
                    _content.text = "Load HiPass";
                    _content.tooltip = "Load highpass values from clipboard";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        ahpf.cutoffFrequency = Single.Parse(hipass_clipboard.GetValue("cutoff_freq"));
                        ahpf.highpassResonaceQ = Single.Parse(hipass_clipboard.GetValue("resonance_q"));
                        if (debugging) Debug.Log("[CHATR] highpass filter values loaded from highpass clipboard");
                    }
                }
                GUILayout.EndHorizontal();
            }
            else if (sel_filter == 4)
            {
                //Lowpass
                _content.text = "Enable";
                _content.tooltip = "Turn lowpass filter on/off";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                alpf.enabled = GUILayout.Toggle(alpf.enabled, _content, GUILayout.ExpandWidth(true));
                _content.text = "Default";
                _content.tooltip = "Reset lowpass filter to default";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) reset_lowpass_filter(alpf);
                GUILayout.EndHorizontal();

                _content.text = "Cutoff freq: " + alpf.cutoffFrequency.ToString("F2") + " Hz";
                _content.tooltip = "Lowpass cutoff frequency in Hz";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                alpf.cutoffFrequency = GUILayout.HorizontalSlider(alpf.cutoffFrequency, 10f, 22000f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Resonance Q: " + alpf.lowpassResonaceQ.ToString("F2");
                _content.tooltip = "Lowpass self-resonance dampening";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                alpf.lowpassResonaceQ = GUILayout.HorizontalSlider(alpf.lowpassResonaceQ, 1f, 10f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                _content.text = "Copy LoPass";
                _content.tooltip = "Copy lowpass values to clipboard";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    lopass_clipboard = new ConfigNode();
                    lopass_clipboard.AddValue("cutoff_freq", alpf.cutoffFrequency);
                    lopass_clipboard.AddValue("resonance_q", alpf.lowpassResonaceQ);
                    if (debugging) Debug.Log("[CHATR] lowpass filter values copied to lowpass clipboard");
                }
                if (lopass_clipboard != null)
                {
                    _content.text = "Load LoPass";
                    _content.tooltip = "Load lowpass values from clipboard";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        alpf.cutoffFrequency = Single.Parse(lopass_clipboard.GetValue("cutoff_freq"));
                        alpf.lowpassResonaceQ = Single.Parse(lopass_clipboard.GetValue("resonance_q"));
                        if (debugging) Debug.Log("[CHATR] lowpass filter values loaded from lowpass clipboard");
                    }
                }
                GUILayout.EndHorizontal();
            }
            else if (sel_filter == 5)
            {
                //Reverb
                _content.text = "Enable";
                _content.tooltip = "Turn reverb filter on/off";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                arf.enabled = GUILayout.Toggle(arf.enabled, _content, GUILayout.ExpandWidth(true));
                _content.text = "Default";
                _content.tooltip = "Reset reverb filter to default";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) reset_reverb_filter(arf);
                GUILayout.EndHorizontal();

                //Presets
                AudioReverbPreset[] preset_array = Enum.GetValues(typeof(AudioReverbPreset)) as AudioReverbPreset[];
                List<AudioReverbPreset> preset_list = new List<AudioReverbPreset>();

                foreach (var val in preset_array)
                {
                    if (val.ToString() != "Off" && val.ToString() != "User") preset_list.Add(val);  //Off and User have separate buttons
                }

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("Current preset: " + arf.reverbPreset.ToString(), GUILayout.ExpandWidth(true));

                //off button
                //_content.text = "Off";
                //_content.tooltip = "Turn reverb off";
                //if (GUILayout.Button(_content, GUILayout.ExpandWidth(true)))
                //{
                //    arf.reverbPreset = AudioReverbPreset.Off;
                //    if (debugging) Debug.Log("[CHATR] reverb turned off");
                //}

                //user button
                _content.text = "User";
                _content.tooltip = "User reverb settings";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    arf.reverbPreset = AudioReverbPreset.User;
                    if (debugging) Debug.Log("[CHATR] reverb set to User");
                }

                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                if (GUILayout.Button("◄", GUILayout.ExpandWidth(true)))
                {
                    //need separate preset indices for chatter and each beep
                    reverb_preset_index--;
                    if (reverb_preset_index < 0) reverb_preset_index = preset_list.Count - 1;
                    if (debugging) Debug.Log("[CHATR] reverb_preset_index = " + reverb_preset_index);
                }
                _content.text = preset_list[reverb_preset_index].ToString();
                _content.tooltip = "Click to apply";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(true)))
                {
                    arf.reverbPreset = preset_list[reverb_preset_index];
                    if (debugging) Debug.Log("[CHATR] reverb preset set to " + arf.reverbPreset.ToString());
                }

                if (GUILayout.Button("►", GUILayout.ExpandWidth(true)))
                {
                    reverb_preset_index++;
                    if (reverb_preset_index >= preset_list.Count) reverb_preset_index = 0;
                    if (debugging) Debug.Log("[CHATR] reverb_preset_index = " + reverb_preset_index);
                }

                GUILayout.EndHorizontal();

                //GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                //GUILayout.Label("source.reverbPreset = " + arf.reverbPreset.ToString());
                //GUILayout.EndHorizontal();

                _content.text = "Dry level: " + arf.dryLevel.ToString("F0") + " mB";
                _content.tooltip = "Mix level of dry signal in output in mB";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.dryLevel = GUILayout.HorizontalSlider(arf.dryLevel, -10000f, 0, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Room: " + arf.room.ToString("F0") + " mB";
                _content.tooltip = "Room effect level at low frequencies in mB";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.room = GUILayout.HorizontalSlider(arf.room, -10000f, 0, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Room HF: " + arf.roomHF.ToString("F0") + " mB";
                _content.tooltip = "Room effect high-frequency level in mB";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.roomHF = GUILayout.HorizontalSlider(arf.roomHF, -10000f, 0, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Room LF: " + arf.roomLF.ToString("F0") + " mB";
                _content.tooltip = "Room effect low-frequency level in mB";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.roomLF = GUILayout.HorizontalSlider(arf.roomLF, -10000f, 0, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Room roll-off: " + arf.roomRolloff.ToString("F2");
                _content.tooltip = "Rolloff factor for room effect";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.roomRolloff = GUILayout.HorizontalSlider(arf.roomRolloff, 0, 10f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Decay time: " + (arf.decayTime * 100).ToString("F0") + " s";
                _content.tooltip = "Reverb decay time at low-frequencies";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.decayTime = GUILayout.HorizontalSlider(arf.decayTime, 0.1f, 20f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Decay HF ratio: " + arf.decayHFRatio.ToString("F0");
                _content.tooltip = "HF to LF decay time ratio";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.decayHFRatio = GUILayout.HorizontalSlider(arf.decayHFRatio, 0.1f, 2f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Reflections level: " + arf.reflectionsLevel.ToString("F0") + " mB";
                _content.tooltip = "Early reflections level relative to room effect";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.reflectionsLevel = GUILayout.HorizontalSlider(arf.reflectionsLevel, -10000f, 1000f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Reflections delay: " + arf.reflectionsDelay.ToString("F0") + " mB";
                _content.tooltip = "Late reverberation level relative to room effect";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.reflectionsDelay = GUILayout.HorizontalSlider(arf.reflectionsDelay, -10000f, 2000f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Reverb level: " + arf.reverbLevel.ToString("F0") + " mB";
                _content.tooltip = "Late reverberation level relative to room effect";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.reverbLevel = GUILayout.HorizontalSlider(arf.reverbLevel, -10000f, 2000f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Reverb delay: " + (arf.reverbDelay * 100).ToString("F0") + " s";
                _content.tooltip = "Late reverb delay time rel. to first reflection";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.reverbDelay = GUILayout.HorizontalSlider(arf.reverbDelay, 0, 0.1f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Diffusion: " + arf.diffusion.ToString("F0") + "%";
                _content.tooltip = "Reverb diffusion (echo density)";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.diffusion = GUILayout.HorizontalSlider(arf.diffusion, 0, 100f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "Density: " + arf.density.ToString("F0") + "%";
                _content.tooltip = "Reverb density (modal density)";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.density = GUILayout.HorizontalSlider(arf.density, 0, 100f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "HF reference: " + arf.hfReference.ToString("F0") + " Hz";
                _content.tooltip = "Reference high frequency in Hz";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.hfReference = GUILayout.HorizontalSlider(arf.hfReference, 20f, 20000f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                _content.text = "LF reference: " + arf.lFReference.ToString("F0") + " Hz";
                _content.tooltip = "Reference low-frequency in Hz";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                arf.lFReference = GUILayout.HorizontalSlider(arf.lFReference, 20f, 1000f, GUILayout.Width(90f));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                _content.text = "Copy Reverb";
                _content.tooltip = "Copy reverb values to clipboard";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    reverb_clipboard = new ConfigNode();
                    reverb_clipboard.AddValue("reverb_preset", arf.reverbPreset);
                    reverb_clipboard.AddValue("dry_level", arf.dryLevel);
                    reverb_clipboard.AddValue("room", arf.room);
                    reverb_clipboard.AddValue("room_hf", arf.roomHF);
                    reverb_clipboard.AddValue("room_lf", arf.roomLF);
                    reverb_clipboard.AddValue("room_rolloff", arf.roomRolloff);
                    reverb_clipboard.AddValue("decay_time", arf.decayTime);
                    reverb_clipboard.AddValue("decay_hf_ratio", arf.decayHFRatio);
                    reverb_clipboard.AddValue("reflections_level", arf.reflectionsLevel);
                    reverb_clipboard.AddValue("reflections_delay", arf.reflectionsDelay);
                    reverb_clipboard.AddValue("reverb_level", arf.reverbLevel);
                    reverb_clipboard.AddValue("reverb_delay", arf.reverbDelay);
                    reverb_clipboard.AddValue("diffusion", arf.diffusion);
                    reverb_clipboard.AddValue("density", arf.density);
                    reverb_clipboard.AddValue("hf_reference", arf.hfReference);
                    reverb_clipboard.AddValue("lf_reference", arf.lFReference);
                    if (debugging) Debug.Log("[CHATR] reverb filter values copied to reverb clipboard");
                }
                if (reverb_clipboard != null)
                {
                    _content.text = "Load Reverb";
                    _content.tooltip = "Load reverb values from clipboard";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        arf.reverbPreset = (AudioReverbPreset)Enum.Parse(typeof(AudioReverbPreset), reverb_clipboard.GetValue("reverb_preset"));
                        arf.dryLevel = Single.Parse(reverb_clipboard.GetValue("dry_level"));
                        arf.room = Single.Parse(reverb_clipboard.GetValue("room"));
                        arf.roomHF = Single.Parse(reverb_clipboard.GetValue("room_hf"));
                        arf.roomLF = Single.Parse(reverb_clipboard.GetValue("room_lf"));
                        arf.roomRolloff = Single.Parse(reverb_clipboard.GetValue("room_rolloff"));
                        arf.decayTime = Single.Parse(reverb_clipboard.GetValue("decay_time"));
                        arf.decayHFRatio = Single.Parse(reverb_clipboard.GetValue("decay_hf_ratio"));
                        arf.reflectionsLevel = Single.Parse(reverb_clipboard.GetValue("reflections_level"));
                        arf.reflectionsDelay = Single.Parse(reverb_clipboard.GetValue("reflections_delay"));
                        arf.reverbLevel = Single.Parse(reverb_clipboard.GetValue("reverb_level"));
                        arf.reverbDelay = Single.Parse(reverb_clipboard.GetValue("reverb_delay"));
                        arf.diffusion = Single.Parse(reverb_clipboard.GetValue("diffusion"));
                        arf.density = Single.Parse(reverb_clipboard.GetValue("density"));
                        arf.hfReference = Single.Parse(reverb_clipboard.GetValue("hf_reference"));
                        arf.lFReference = Single.Parse(reverb_clipboard.GetValue("lf_reference"));
                        if (debugging) Debug.Log("[CHATR] reverb filter values loaded from reverb clipboard");
                    }
                }
                GUILayout.EndHorizontal();
            }
            return reverb_preset_index;
        }

        private void probe_sample_selector_gui(int window_id)
        {
            GUIContent _content = new GUIContent();

            BeepSource source = beepsource_list[(((sel_beep_page - 1) * 10) + sel_beep_src)];   //shortcut   0-9 only, but correspond to the correct beepsource

            //GUILayout.Label("Beepsource " + source.beep_name, label_txt_center);

            probe_sample_selector_scroll_pos = GUILayout.BeginScrollView(probe_sample_selector_scroll_pos, false, true);

            //list each sample from Dict
            foreach (string key in dict_probe_samples.Keys)
            {
                AudioClip _clip = new AudioClip();
                GUIStyle sample_gs = label_txt_left;

                if (dict_probe_samples.TryGetValue(key, out _clip))
                {

                    //check if _clip is == source.clip
                    //if yes, bold it
                    if (_clip == source.audiosource.clip) sample_gs = label_txt_bold;
                    //else sample_gs = label_txt_left;
                }

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                _content.text = key;
                _content.tooltip = "Probe sample file name";
                GUILayout.Label(_content, sample_gs, GUILayout.ExpandWidth(true));

                _content.text = "►";
                _content.tooltip = "Play this sample once";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    if ((exchange_playing && disable_beeps_during_chatter) || sstv.isPlaying) return;   //don't play during chatter or sstv
                    //if (debugging) Debug.Log("[CHATR] playing sample " + source.current_clip + " one time...");




                    OTP_source = source;
                    OTP_stored_clip = source.audiosource.clip;



                    //if (debugging) Debug.Log("[CHATR] OTP_stored_clip = " + OTP_stored_clip);
                    //source.current_clip = key;
                    //if (debugging) Debug.Log("[CHATR] set clip " + source.current_clip + " to play once");
                    //set_beep_clip(source);
                    //if (debugging) Debug.Log("[CHATR] source.audiosource.clip set");

                    //AudioClip _clip;
                    if (dict_probe_samples.TryGetValue(key, out _clip))
                    {
                        source.audiosource.clip = _clip;
                    }

                    OTP_playing = true;
                    source.audiosource.Play();

                    //problem may be right here when setting clip back right after playing
                    //reset clip in Update() after playing has finished



                    //if (debugging) Debug.Log("[CHATR] AudioSource has played");
                    //source.current_clip = stored_clip;
                    //if (debugging) Debug.Log("[CHATR] source.current_clip = " +  source.current_clip);
                    //set_beep_clip(source);

                    //AudioClip temp_clip;
                    //if (dict_probe_samples.TryGetValue(key, out temp_clip))
                    //{
                    //    if (debugging) Debug.Log("[CHATR] got temp_clip, key = " + key);
                    //    source.audiosource.clip = temp_clip;
                    //    if (debugging) Debug.Log("[CHATR] playing one time");
                    //    source.audiosource.Play();
                    //    source.current_clip = stored_clip;
                    //    set_beep_clip(source);
                    //    if (debugging) Debug.Log("[CHATR] stored clip replaced");
                    //}
                }

                _content.text = "Set";
                _content.tooltip = "Set this sample to play from this beepsource";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    //sample was selected
                    source.current_clip = key;  //set current_clip
                    set_beep_clip(source);  //then assign AudioClip
                    if (debugging) Debug.Log("[CHATR] sample selector clip set :: clip = " + key);

                    //set and play once when clicked
                    //if ((exchange_playing && disable_beeps_during_chatter) || sstv.isPlaying) return;   //don't play during chatter or sstv
                    //else
                    //{
                    //if (debugging) Debug.Log("[CHATR] playing sample " + source.current_clip + " one time...");
                    //source.audiosource.clip = all_beep_clips[bm.current_clip - 1];
                    //source.audiosource.Play();
                    //}

                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label(" ", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Close", GUILayout.ExpandWidth(false)))
            {
                show_probe_sample_selector = false;
            }
            GUILayout.EndHorizontal();

            if (show_tooltips && GUI.tooltip != "") tooltips(probe_sample_selector_window_pos);

            GUI.DragWindow();
        }

        private void AAE_background_sample_selector_gui(int window_id)
        {
            GUIContent _content = new GUIContent();

            //BeepSource source = beepsource_list[(((sel_beep_page - 1) * 10) + sel_beep_src)];   //shortcut   0-9 only, but correspond to the correct beepsource

            BackgroundSource src = sel_background_src;

            AAE_background_sample_selector_scroll_pos = GUILayout.BeginScrollView(AAE_background_sample_selector_scroll_pos, false, true);

            //list each sample from Dict
            foreach (string key in dict_background_samples.Keys)
            {
                AudioClip _clip = new AudioClip();
                GUIStyle sample_gs = label_txt_left;

                if (dict_background_samples.TryGetValue(key, out _clip))
                {

                    //check if _clip is == source.clip
                    //if yes, bold it
                    if (_clip == src.audiosource.clip) sample_gs = label_txt_bold;
                    else sample_gs = label_txt_left;
                }

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                _content.text = key;
                _content.tooltip = "Background sample file name";
                GUILayout.Label(_content, sample_gs, GUILayout.ExpandWidth(true));

                /*
                _content.text = "►";
                _content.tooltip = "Play this sample once";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    if ((exchange_playing && disable_beeps_during_chatter) || sstv.isPlaying) return;   //don't play during chatter or sstv
                    //if (debugging) Debug.Log("[CHATR] playing sample " + source.current_clip + " one time...");




                    OTP_source = source;
                    OTP_stored_clip = source.audiosource.clip;



                    //if (debugging) Debug.Log("[CHATR] OTP_stored_clip = " + OTP_stored_clip);
                    //source.current_clip = key;
                    //if (debugging) Debug.Log("[CHATR] set clip " + source.current_clip + " to play once");
                    //set_beep_clip(source);
                    //if (debugging) Debug.Log("[CHATR] source.audiosource.clip set");

                    //AudioClip _clip;
                    if (dict_probe_samples.TryGetValue(key, out _clip))
                    {
                        source.audiosource.clip = _clip;
                    }

                    OTP_playing = true;
                    source.audiosource.Play();

                    //problem may be right here when setting clip back right after playing
                    //reset clip in Update() after playing has finished



                    //if (debugging) Debug.Log("[CHATR] AudioSource has played");
                    //source.current_clip = stored_clip;
                    //if (debugging) Debug.Log("[CHATR] source.current_clip = " +  source.current_clip);
                    //set_beep_clip(source);

                    //AudioClip temp_clip;
                    //if (dict_probe_samples.TryGetValue(key, out temp_clip))
                    //{
                    //    if (debugging) Debug.Log("[CHATR] got temp_clip, key = " + key);
                    //    source.audiosource.clip = temp_clip;
                    //    if (debugging) Debug.Log("[CHATR] playing one time");
                    //    source.audiosource.Play();
                    //    source.current_clip = stored_clip;
                    //    set_beep_clip(source);
                    //    if (debugging) Debug.Log("[CHATR] stored clip replaced");
                    //}
                }
                */

                _content.text = "Set";
                _content.tooltip = "Set this sample to play from this backgroundsource";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    //sample was selected
                    src.current_clip = key;  //set current_clip
                    //set_beep_clip(source);  //then assign AudioClip

                    AudioClip temp_clip = new AudioClip();

                    if (dict_background_samples.TryGetValue(src.current_clip, out temp_clip))
                    {
                        src.audiosource.clip = temp_clip;
                        string s = "";
                        if (dict_background_samples2.TryGetValue(src.audiosource.clip, out s))
                        {
                            src.current_clip = s;
                            if (debugging) Debug.Log("[CHATR] background AudioClip set :: current_clip = " + s);
                        }
                    }
                    else
                    {
                        if (debugging) Debug.LogError("[CHATR] Could not find AudioClip for key " + src.current_clip + " :: setting AudioClip to \"First\"");
                        src.current_clip = "First";
                        set_background_clip(src);
                        //set_beep_clip(beepsource);
                    }




                    if (debugging) Debug.Log("[CHATR] sample selector clip set :: clip = " + key);

                    //set and play once when clicked
                    //if ((exchange_playing && disable_beeps_during_chatter) || sstv.isPlaying) return;   //don't play during chatter or sstv
                    //else
                    //{
                    //if (debugging) Debug.Log("[CHATR] playing sample " + source.current_clip + " one time...");
                    //source.audiosource.clip = all_beep_clips[bm.current_clip - 1];
                    //source.audiosource.Play();
                    //}

                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label(" ", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Close", GUILayout.ExpandWidth(false)))
            {
                show_AAE_background_sample_selector = false;
            }
            GUILayout.EndHorizontal();

            if (show_tooltips && GUI.tooltip != "") tooltips(probe_sample_selector_window_pos);

            GUI.DragWindow();
        }

        //Set audioclip
        private void set_beep_clip(BeepSource beepsource)
        {
            if (beepsource.current_clip == "First")
            {
                //"First" is used when creating a new beepsource
                //if (debugging) Debug.Log("[CHATR] beep AudioClip is \"First\"");
                //pick any AudioClip (when adding a new beepsource)

                //dump all values into a List
                //get a random index for that list
                //assign
                List<AudioClip> val_list = new List<AudioClip>();
                foreach (AudioClip val in dict_probe_samples.Values)
                {
                    val_list.Add(val);
                }
                beepsource.audiosource.clip = val_list[0];
                string s = "";
                if (dict_probe_samples2.TryGetValue(beepsource.audiosource.clip, out s))
                {
                    beepsource.current_clip = s;
                    if (debugging) Debug.Log("[CHATR] \"First\" AudioClip set :: current_clip = " + s);
                }
            }
            else if (beepsource.current_clip == "Random")
            {
                if (debugging) Debug.Log("[CHATR] setting random AudioClip...");
                List<AudioClip> clip_list = new List<AudioClip>();
                foreach (AudioClip clip in dict_probe_samples.Values)
                {
                    clip_list.Add(clip);
                }
                beepsource.audiosource.clip = clip_list[rand.Next(0, clip_list.Count)];
                string s = "";
                if (dict_probe_samples2.TryGetValue(beepsource.audiosource.clip, out s))
                {
                    beepsource.current_clip = s;
                    if (debugging) Debug.Log("[CHATR] beep AudioClip randomized :: current_clip = " + s);
                }
            }
            else
            {
                AudioClip temp_clip = new AudioClip();

                //broken here current_clip == null

                if (dict_probe_samples.TryGetValue(beepsource.current_clip, out temp_clip))
                {
                    beepsource.audiosource.clip = temp_clip;
                    string s = "";
                    if (dict_probe_samples2.TryGetValue(beepsource.audiosource.clip, out s))
                    {
                        beepsource.current_clip = s;
                        if (debugging) Debug.Log("[CHATR] beep AudioClip set :: current_clip = " + s);
                    }
                }
                else
                {
                    if (debugging) Debug.LogError("[CHATR] Could not find AudioClip for key " + beepsource.current_clip + " :: setting AudioClip to \"First\"");
                    beepsource.current_clip = "First";
                    set_beep_clip(beepsource);
                }
            }
        }

        private void set_background_clip(BackgroundSource src)
        {
            //
            //FIX background audio clip assignment.  i think it will fuck up if there are less than 2 clips found
            //

            if (src.current_clip == "Default")
            {
                //build a list of all background clips
                List<AudioClip> val_list = new List<AudioClip>();
                foreach (AudioClip val in dict_background_samples.Values)
                {
                    val_list.Add(val);
                }

                //set current source clip from above list (first source gets first clip, second gets second, etc)
                src.audiosource.clip = val_list[backgroundsource_list.Count - 1];

                //get the file name for the clip
                string s = "";
                if (dict_background_samples2.TryGetValue(src.audiosource.clip, out s))
                {
                    src.current_clip = s;
                    if (debugging) Debug.Log("[CHATR] \"Default\" AudioClip set :: current_clip = " + s);
                }
            }
        }

        private void set_soundscape_clip()
        {
            //create a new List using Values from dictionary
            List<AudioClip> clips = new List<AudioClip>(dict_soundscape_samples.Values);
            aae_soundscape.clip = clips[rand.Next(0, clips.Count)];

            //get the file name for the clip
            string s = "";
            if (dict_soundscape_samples2.TryGetValue(aae_soundscape.clip, out s))
            {
                aae_soundscape_current_clip = s;
                if (debugging) Debug.Log("[CHATR] Soundscape AudioClip set :: current_clip = " + s);
            }
        }

        //Create/destroy sources
        private void add_new_beepsource()
        {
            beepsource_list.Add(new BeepSource());

            int x = beepsource_list.Count - 1;

            beepsource_list[x].beep_player = new GameObject();
            beepsource_list[x].beep_player.name = "rbr_beep_player_" + beepsource_list.Count;
            beepsource_list[x].beep_name = beepsource_list.Count.ToString();
            beepsource_list[x].audiosource = beepsource_list[x].beep_player.AddComponent<AudioSource>();
            beepsource_list[x].audiosource.volume = 0.3f;   //default 30%
            beepsource_list[x].audiosource.panLevel = 0;
            //beepsource_list[x].audiosource.clip = all_beep_clips[0];
            beepsource_list[x].current_clip = "First";
            beepsource_list[x].chorus_filter = beepsource_list[x].beep_player.AddComponent<AudioChorusFilter>();
            beepsource_list[x].chorus_filter.enabled = false;
            beepsource_list[x].distortion_filter = beepsource_list[x].beep_player.AddComponent<AudioDistortionFilter>();
            beepsource_list[x].distortion_filter.enabled = false;
            beepsource_list[x].echo_filter = beepsource_list[x].beep_player.AddComponent<AudioEchoFilter>();
            beepsource_list[x].echo_filter.enabled = false;
            beepsource_list[x].highpass_filter = beepsource_list[x].beep_player.AddComponent<AudioHighPassFilter>();
            beepsource_list[x].highpass_filter.enabled = false;
            beepsource_list[x].lowpass_filter = beepsource_list[x].beep_player.AddComponent<AudioLowPassFilter>();
            beepsource_list[x].lowpass_filter.enabled = false;
            beepsource_list[x].reverb_filter = beepsource_list[x].beep_player.AddComponent<AudioReverbFilter>();
            beepsource_list[x].reverb_filter.enabled = false;

            if (dict_probe_samples.Count > 0)
            {
                set_beep_clip(beepsource_list[x]);   //set

                if (beepsource_list[x].precise == false && beepsource_list[x].loose_freq > 0) new_beep_loose_timer_limit(beepsource_list[x]);
            }
        }

        private void add_new_backgroundsource()
        {
            backgroundsource_list.Add(new BackgroundSource());

            int x = backgroundsource_list.Count - 1;

            backgroundsource_list[x].background_player = new GameObject();
            backgroundsource_list[x].background_player.name = "rbr_background_player_" + backgroundsource_list.Count;
            backgroundsource_list[x].audiosource = backgroundsource_list[x].background_player.AddComponent<AudioSource>();
            backgroundsource_list[x].audiosource.volume = 0.3f;
            backgroundsource_list[x].audiosource.panLevel = 0;
            backgroundsource_list[x].current_clip = "Default";

            if (dict_background_samples.Count > 0)
            {
                set_background_clip(backgroundsource_list[x]);  //set clip
            }
        }

        private void destroy_all_beep_players()
        {
            var allSources = FindObjectsOfType(typeof(GameObject)) as GameObject[];
            List<GameObject> temp = new List<GameObject>();
            string search_str = "rbr_beep";
            int search_str_len = search_str.Length;

            foreach (var source in allSources)
            {
                if (source.name.Length > search_str_len)
                {
                    if (source.name.Substring(0, search_str_len) == search_str)
                    {
                        if (debugging) Debug.Log("[CHATR] destroying " + source.name);
                        Destroy(source);
                    }
                }
            }
        }

        private void destroy_all_background_players()
        {
            var allSources = FindObjectsOfType(typeof(GameObject)) as GameObject[];
            List<GameObject> temp = new List<GameObject>();
            string search_str = "rbr_background";
            int search_str_len = search_str.Length;

            foreach (var source in allSources)
            {
                if (source.name.Length > search_str_len)
                {
                    if (source.name.Substring(0, search_str_len) == search_str)
                    {
                        if (debugging) Debug.Log("[CHATR] destroying " + source.name);
                        Destroy(source);
                    }
                }
            }
        }

        //Save/Load settings
        private void save_plugin_settings()
        {
            //these values are not saved to vessel.cfg ever and are considered global
            //if (debugging) Debug.Log("[CHATR] adding plugin settings to ConfigNode for write");
            plugin_settings_node = new ConfigNode();
            plugin_settings_node.name = "SETTINGS";
            plugin_settings_node.AddValue("debugging", debugging);
            plugin_settings_node.AddValue("use_vessel_settings", use_vessel_settings);
            plugin_settings_node.AddValue("useBlizzy78Toolbar", useBlizzy78Toolbar);
            plugin_settings_node.AddValue("http_update_check", http_update_check);
            plugin_settings_node.AddValue("disable_beeps_during_chatter", disable_beeps_during_chatter);
            plugin_settings_node.AddValue("insta_chatter_key", insta_chatter_key);
            plugin_settings_node.AddValue("insta_sstv_key", insta_sstv_key);
            plugin_settings_node.AddValue("show_advanced_options", show_advanced_options);

            //also save values that are shared between the two configs
            save_shared_settings(plugin_settings_node);

            //save plugin.cfg
            plugin_settings_node.Save(settings_path + "plugin.cfg");
            if (debugging) Debug.Log("[CHATR] plugin settings saved to disk");

            //update vessel_settings.cfg
            if (use_vessel_settings)
            {
                write_vessel_settings();
                if (debugging) Debug.Log("[CHATR] this vessel settings saved to disk");
            }
        }

        private void load_plugin_settings()
        {
            if (debugging) Debug.Log("[CHATR] load_plugin_settings() START");

            destroy_all_beep_players();
            chatter_array.Clear();
            beepsource_list.Clear();

            plugin_settings_node = new ConfigNode();
            plugin_settings_node = ConfigNode.Load(settings_path + "plugin.cfg");

            if (plugin_settings_node != null)
            {
                if (debugging) Debug.Log("[CHATR] plugin_settings != null");
                //Load settings specific to plugin.cfg
                if (plugin_settings_node.HasValue("debugging")) debugging = Boolean.Parse(plugin_settings_node.GetValue("debugging"));
                if (plugin_settings_node.HasValue("use_vessel_settings")) use_vessel_settings = Boolean.Parse(plugin_settings_node.GetValue("use_vessel_settings"));
                if (plugin_settings_node.HasValue("useBlizzy78Toolbar")) useBlizzy78Toolbar = Boolean.Parse(plugin_settings_node.GetValue("useBlizzy78Toolbar"));
                if (plugin_settings_node.HasValue("http_update_check")) http_update_check = Boolean.Parse(plugin_settings_node.GetValue("http_update_check"));
                if (plugin_settings_node.HasValue("disable_beeps_during_chatter")) disable_beeps_during_chatter = Boolean.Parse(plugin_settings_node.GetValue("disable_beeps_during_chatter"));
                if (plugin_settings_node.HasValue("insta_chatter_key")) insta_chatter_key = (KeyCode)Enum.Parse(typeof(KeyCode), plugin_settings_node.GetValue("insta_chatter_key"));
                if (plugin_settings_node.HasValue("insta_sstv_key")) insta_sstv_key = (KeyCode)Enum.Parse(typeof(KeyCode), plugin_settings_node.GetValue("insta_sstv_key"));
                if (plugin_settings_node.HasValue("show_advanced_options")) show_advanced_options = Boolean.Parse(plugin_settings_node.GetValue("show_advanced_options"));

                load_shared_settings(plugin_settings_node); //load settings shared between both configs

            }
            else
            {
                if (debugging) Debug.LogWarning("[CHATR] plugin.cfg missing or unreadable");
            }

            //if (chatter_exists && chatter_array.Count == 0)
            if (chatter_array.Count == 0)
            {
                if (debugging) Debug.Log("[CHATR] No audiosets found in config, adding defaults");
                add_default_audiosets();
                load_chatter_audio();   //load audio in case there is none
            }

            if (beeps_exists && beepsource_list.Count == 0)
            {
                if (debugging) Debug.LogWarning("[CHATR] beepsource_list.Count == 0, adding default 3");
                add_default_beepsources();
            }

            if (aae_backgrounds_exist && backgroundsource_list.Count == 0)
            {
                if (debugging) Debug.LogWarning("[CHATR] backgroundsource_list.Count == 0, adding default 2");
                add_default_backgroundsources();
            }

            if (debugging) Debug.Log("[CHATR] load_plugin_settings() END");
        }

        private void load_plugin_defaults()
        {
            if (debugging) Debug.Log("[CHATR] load_plugin_defaults() START");

            destroy_all_beep_players();
            chatter_array.Clear();
            beepsource_list.Clear();

            plugin_settings_node = new ConfigNode();
            plugin_settings_node = ConfigNode.Load(settings_path + "plugin_defaults.cfg");

            if (plugin_settings_node != null)
            {
                if (debugging) Debug.Log("[CHATR] plugin_defaults != null");
                //Load settings specific to plugin.cfg
                if (plugin_settings_node.HasValue("debugging")) debugging = Boolean.Parse(plugin_settings_node.GetValue("debugging"));
                if (plugin_settings_node.HasValue("use_vessel_settings")) use_vessel_settings = Boolean.Parse(plugin_settings_node.GetValue("use_vessel_settings"));
                if (plugin_settings_node.HasValue("useBlizzy78Toolbar")) useBlizzy78Toolbar = Boolean.Parse(plugin_settings_node.GetValue("useBlizzy78Toolbar"));
                if (plugin_settings_node.HasValue("http_update_check")) http_update_check = Boolean.Parse(plugin_settings_node.GetValue("http_update_check"));
                if (plugin_settings_node.HasValue("disable_beeps_during_chatter")) disable_beeps_during_chatter = Boolean.Parse(plugin_settings_node.GetValue("disable_beeps_during_chatter"));
                if (plugin_settings_node.HasValue("insta_chatter_key")) insta_chatter_key = (KeyCode)Enum.Parse(typeof(KeyCode), plugin_settings_node.GetValue("insta_chatter_key"));
                if (plugin_settings_node.HasValue("insta_sstv_key")) insta_sstv_key = (KeyCode)Enum.Parse(typeof(KeyCode), plugin_settings_node.GetValue("insta_sstv_key"));
                if (plugin_settings_node.HasValue("show_advanced_options")) show_advanced_options = Boolean.Parse(plugin_settings_node.GetValue("show_advanced_options"));

                load_shared_settings(plugin_settings_node); //load settings shared between both configs

            }
            else
            {
                if (debugging) Debug.LogWarning("[CHATR] plugin_defautls.cfg missing or unreadable");
            }

            //if (chatter_exists && chatter_array.Count == 0)
            if (chatter_array.Count == 0)
            {
                if (debugging) Debug.Log("[CHATR] No audiosets found in config, adding defaults");
                add_default_audiosets();
                load_chatter_audio();   //load audio in case there is none
            }

            if (beeps_exists && beepsource_list.Count == 0)
            {
                if (debugging) Debug.LogWarning("[CHATR] beepsource_list.Count == 0, adding default 3");
                add_default_beepsources();
            }

            if (aae_backgrounds_exist && backgroundsource_list.Count == 0)
            {
                if (debugging) Debug.LogWarning("[CHATR] backgroundsource_list.Count == 0, adding default 2");
                add_default_backgroundsources();
            }

            if (debugging) Debug.Log("[CHATR] load_plugin_defaults() END");
        }

        //Functions to handle settings shared by plugin.cfg and vessel.cfg
        private void save_shared_settings(ConfigNode node)
        {
            node.AddValue("show_tooltips", show_tooltips);
            node.AddValue("main_window_pos", main_window_pos.x + "," + main_window_pos.y);
            node.AddValue("hide_all_windows", hide_all_windows);
            node.AddValue("skin_index", skin_index);
            node.AddValue("active_menu", active_menu);
            node.AddValue("remotetech_toggle", remotetech_toggle);

            node.AddValue("chatter_freq", chatter_freq);
            node.AddValue("chatter_vol_slider", chatter_vol_slider);
            node.AddValue("chatter_sel_filter", chatter_sel_filter);
            node.AddValue("show_chatter_filter_settings", show_chatter_filter_settings);
            node.AddValue("show_sample_selector", show_probe_sample_selector);
            node.AddValue("chatter_reverb_preset_index", chatter_reverb_preset_index);
            node.AddValue("chatter_filter_settings_window_pos", chatter_filter_settings_window_pos.x + "," + chatter_filter_settings_window_pos.y);
            node.AddValue("probe_sample_selector_window_pos", probe_sample_selector_window_pos.x + "," + probe_sample_selector_window_pos.y);

            node.AddValue("quindar_toggle", quindar_toggle);
            node.AddValue("quindar_vol_slider", quindar_vol_slider);
            node.AddValue("sstv_freq", sstv_freq);
            node.AddValue("sstv_vol_slider", sstv_vol_slider);

            node.AddValue("sel_beep_src", sel_beep_src);
            node.AddValue("sel_beep_page", sel_beep_page);

            //AAE
            if (aae_backgrounds_exist)
            {
                foreach (BackgroundSource src in backgroundsource_list)
                {
                    ConfigNode _background = new ConfigNode();
                    _background.name = "AAE_BACKGROUND";
                    _background.AddValue("volume", src.audiosource.volume);
                    _background.AddValue("current_clip", src.current_clip);
                    node.AddNode(_background);
                }
            }

            if (aae_soundscapes_exist)
            {
                node.AddValue("aae_soundscape_vol", aae_soundscape.volume);
                node.AddValue("aae_soundscape_freq", aae_soundscape_freq);
            }

            if (aae_breathing_exist) node.AddValue("aae_breathing_vol", aae_breathing.volume);
            if (aae_wind_exist) node.AddValue("aae_wind_vol", aae_wind_vol_slider);
            if (aae_airlock_exist) node.AddValue("aae_airlock_vol", aae_airlock.volume);

            //Chatter sets
            foreach (ChatterAudioList chatter_set in chatter_array)
            {
                ConfigNode _set = new ConfigNode();
                _set.name = "AUDIOSET";
                _set.AddValue("directory", chatter_set.directory);
                _set.AddValue("is_active", chatter_set.is_active);
                node.AddNode(_set);
            }

            //filters
            ConfigNode _filter;
            _filter = new ConfigNode();
            _filter.name = "CHORUS";
            _filter.AddValue("enabled", chatter_chorus_filter.enabled);
            _filter.AddValue("dry_mix", chatter_chorus_filter.dryMix);
            _filter.AddValue("wet_mix_1", chatter_chorus_filter.wetMix1);
            _filter.AddValue("wet_mix_2", chatter_chorus_filter.wetMix2);
            _filter.AddValue("wet_mix_3", chatter_chorus_filter.wetMix3);
            _filter.AddValue("delay", chatter_chorus_filter.delay);
            _filter.AddValue("rate", chatter_chorus_filter.rate);
            _filter.AddValue("depth", chatter_chorus_filter.depth);
            node.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "DISTORTION";
            _filter.AddValue("enabled", chatter_distortion_filter.enabled);
            _filter.AddValue("distortion_level", chatter_distortion_filter.distortionLevel);
            node.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "ECHO";
            _filter.AddValue("enabled", chatter_echo_filter.enabled);
            _filter.AddValue("delay", chatter_echo_filter.delay);
            _filter.AddValue("decay_ratio", chatter_echo_filter.decayRatio);
            _filter.AddValue("dry_mix", chatter_echo_filter.dryMix);
            _filter.AddValue("wet_mix", chatter_echo_filter.wetMix);
            node.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "HIGHPASS";
            _filter.AddValue("enabled", chatter_highpass_filter.enabled);
            _filter.AddValue("cutoff_freq", chatter_highpass_filter.cutoffFrequency);
            _filter.AddValue("resonance_q", chatter_highpass_filter.highpassResonaceQ);
            node.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "LOWPASS";
            _filter.AddValue("enabled", chatter_lowpass_filter.enabled);
            _filter.AddValue("cutoff_freq", chatter_lowpass_filter.cutoffFrequency);
            _filter.AddValue("resonance_q", chatter_lowpass_filter.lowpassResonaceQ);
            node.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "REVERB";
            _filter.AddValue("enabled", chatter_reverb_filter.enabled);
            _filter.AddValue("reverb_preset", chatter_reverb_filter.reverbPreset);
            _filter.AddValue("dry_level", chatter_reverb_filter.dryLevel);
            _filter.AddValue("room", chatter_reverb_filter.room);
            _filter.AddValue("room_hf", chatter_reverb_filter.roomHF);
            _filter.AddValue("room_lf", chatter_reverb_filter.roomLF);
            _filter.AddValue("room_rolloff", chatter_reverb_filter.roomRolloff);
            _filter.AddValue("decay_time", chatter_reverb_filter.decayTime);
            _filter.AddValue("decay_hf_ratio", chatter_reverb_filter.decayHFRatio);
            _filter.AddValue("reflections_level", chatter_reverb_filter.reflectionsLevel);
            _filter.AddValue("reflections_delay", chatter_reverb_filter.reflectionsDelay);
            _filter.AddValue("reverb_level", chatter_reverb_filter.reverbLevel);
            _filter.AddValue("reverb_delay", chatter_reverb_filter.reverbDelay);
            _filter.AddValue("diffusion", chatter_reverb_filter.diffusion);
            _filter.AddValue("density", chatter_reverb_filter.density);
            _filter.AddValue("hf_reference", chatter_reverb_filter.hfReference);
            _filter.AddValue("lf_reference", chatter_reverb_filter.lFReference);
            node.AddNode(_filter);


            foreach (BeepSource source in beepsource_list)
            {
                ConfigNode beep_settings = new ConfigNode();
                beep_settings.name = "BEEPSOURCE";

                beep_settings.AddValue("precise", source.precise);
                beep_settings.AddValue("precise_freq", source.precise_freq);
                beep_settings.AddValue("loose_freq", source.loose_freq);
                beep_settings.AddValue("volume", source.audiosource.volume);
                beep_settings.AddValue("pitch", source.audiosource.pitch);
                beep_settings.AddValue("current_clip", source.current_clip);
                beep_settings.AddValue("sel_filter", source.sel_filter);
                beep_settings.AddValue("show_settings_window", source.show_settings_window);
                beep_settings.AddValue("reverb_preset_index", source.reverb_preset_index);
                beep_settings.AddValue("settings_window_pos_x", source.settings_window_pos.x);
                beep_settings.AddValue("settings_window_pos_y", source.settings_window_pos.y);

                //filters
                //ConfigNode _filter;

                _filter = new ConfigNode();
                _filter.name = "CHORUS";
                _filter.AddValue("enabled", source.chorus_filter.enabled);
                _filter.AddValue("dry_mix", source.chorus_filter.dryMix);
                _filter.AddValue("wet_mix_1", source.chorus_filter.wetMix1);
                _filter.AddValue("wet_mix_2", source.chorus_filter.wetMix2);
                _filter.AddValue("wet_mix_3", source.chorus_filter.wetMix3);
                _filter.AddValue("delay", source.chorus_filter.delay);
                _filter.AddValue("rate", source.chorus_filter.rate);
                _filter.AddValue("depth", source.chorus_filter.depth);
                beep_settings.AddNode(_filter);

                _filter = new ConfigNode();
                _filter.name = "DISTORTION";
                _filter.AddValue("enabled", source.distortion_filter.enabled);
                _filter.AddValue("distortion_level", source.distortion_filter.distortionLevel);
                beep_settings.AddNode(_filter);

                _filter = new ConfigNode();
                _filter.name = "ECHO";
                _filter.AddValue("enabled", source.echo_filter.enabled);
                _filter.AddValue("delay", source.echo_filter.delay);
                _filter.AddValue("decay_ratio", source.echo_filter.decayRatio);
                _filter.AddValue("dry_mix", source.echo_filter.dryMix);
                _filter.AddValue("wet_mix", source.echo_filter.wetMix);
                beep_settings.AddNode(_filter);

                _filter = new ConfigNode();
                _filter.name = "HIGHPASS";
                _filter.AddValue("enabled", source.highpass_filter.enabled);
                _filter.AddValue("cutoff_freq", source.highpass_filter.cutoffFrequency);
                _filter.AddValue("resonance_q", source.highpass_filter.highpassResonaceQ);
                beep_settings.AddNode(_filter);

                _filter = new ConfigNode();
                _filter.name = "LOWPASS";
                _filter.AddValue("enabled", source.lowpass_filter.enabled);
                _filter.AddValue("cutoff_freq", source.lowpass_filter.cutoffFrequency);
                _filter.AddValue("resonance_q", source.lowpass_filter.lowpassResonaceQ);
                beep_settings.AddNode(_filter);

                _filter = new ConfigNode();
                _filter.name = "REVERB";
                _filter.AddValue("enabled", source.reverb_filter.enabled);
                _filter.AddValue("reverb_preset", source.reverb_filter.reverbPreset);
                _filter.AddValue("dry_level", source.reverb_filter.dryLevel);
                _filter.AddValue("room", source.reverb_filter.room);
                _filter.AddValue("room_hf", source.reverb_filter.roomHF);
                _filter.AddValue("room_lf", source.reverb_filter.roomLF);
                _filter.AddValue("room_rolloff", source.reverb_filter.roomRolloff);
                _filter.AddValue("decay_time", source.reverb_filter.decayTime);
                _filter.AddValue("decay_hf_ratio", source.reverb_filter.decayHFRatio);
                _filter.AddValue("reflections_level", source.reverb_filter.reflectionsLevel);
                _filter.AddValue("reflections_delay", source.reverb_filter.reflectionsDelay);
                _filter.AddValue("reverb_level", source.reverb_filter.reverbLevel);
                _filter.AddValue("reverb_delay", source.reverb_filter.reverbDelay);
                _filter.AddValue("diffusion", source.reverb_filter.diffusion);
                _filter.AddValue("density", source.reverb_filter.density);
                _filter.AddValue("hf_reference", source.reverb_filter.hfReference);
                _filter.AddValue("lf_reference", source.reverb_filter.lFReference);
                beep_settings.AddNode(_filter);

                node.AddNode(beep_settings);
            }
        }

        private void load_shared_settings(ConfigNode node)
        {
            if (debugging) Debug.Log("[CHATR] load_shared_settings() START");

            destroy_all_beep_players();
            beepsource_list.Clear();
            destroy_all_background_players();
            backgroundsource_list.Clear();
            chatter_array.Clear();

            if (node.HasValue("main_window_pos"))
            {
                string[] split = node.GetValue("main_window_pos").Split(Convert.ToChar(","));
                main_window_pos.x = Single.Parse(split[0]);
                main_window_pos.y = Single.Parse(split[1]);
            }
            
            if (node.HasValue("show_tooltips")) show_tooltips = Boolean.Parse(node.GetValue("show_tooltips"));
            if (node.HasValue("hide_all_windows")) hide_all_windows = Boolean.Parse(node.GetValue("hide_all_windows"));
            if (node.HasValue("skin_index")) skin_index = Int32.Parse(node.GetValue("skin_index"));
            if (node.HasValue("active_menu")) active_menu = Int32.Parse(node.GetValue("active_menu"));
            if (node.HasValue("remotetech_toggle")) remotetech_toggle = Boolean.Parse(node.GetValue("remotetech_toggle"));

            if (node.HasValue("chatter_freq"))
            {
                chatter_freq = Int32.Parse(node.GetValue("chatter_freq"));
                chatter_freq_slider = chatter_freq;
                prev_chatter_freq = chatter_freq;
            }
            if (node.HasValue("chatter_vol_slider"))
            {
                chatter_vol_slider = Single.Parse(node.GetValue("chatter_vol_slider"));
                initial_chatter.volume = chatter_vol_slider;
                response_chatter.volume = chatter_vol_slider;
                prev_chatter_vol_slider = chatter_vol_slider;
            }
            if (node.HasValue("chatter_sel_filter")) chatter_sel_filter = Int32.Parse(node.GetValue("chatter_sel_filter"));
            if (node.HasValue("show_chatter_filter_settings")) show_chatter_filter_settings = Boolean.Parse(node.GetValue("show_chatter_filter_settings"));
            if (node.HasValue("chatter_reverb_preset_index")) chatter_reverb_preset_index = Int32.Parse(node.GetValue("chatter_reverb_preset_index"));
            if (node.HasValue("chatter_filter_settings_window_pos"))
            {
                string[] split = node.GetValue("chatter_filter_settings_window_pos").Split(Convert.ToChar(","));
                chatter_filter_settings_window_pos.x = Single.Parse(split[0]);
                chatter_filter_settings_window_pos.y = Single.Parse(split[1]);
            }
            if (node.HasValue("show_sample_selector")) show_probe_sample_selector = Boolean.Parse(node.GetValue("show_sample_selector"));
            if (node.HasValue("probe_sample_selector_window_pos"))
            {
                string[] split = node.GetValue("probe_sample_selector_window_pos").Split(Convert.ToChar(","));
                probe_sample_selector_window_pos.x = Single.Parse(split[0]);
                probe_sample_selector_window_pos.y = Single.Parse(split[1]);
            }
            if (node.HasValue("quindar_toggle")) quindar_toggle = Boolean.Parse(node.GetValue("quindar_toggle"));
            if (node.HasValue("quindar_vol_slider"))
            {
                quindar_vol_slider = Single.Parse(node.GetValue("quindar_vol_slider"));
                prev_quindar_vol_slider = quindar_vol_slider;
            }
            if (node.HasValue("sstv_freq"))
            {
                sstv_freq = Int32.Parse(node.GetValue("sstv_freq"));
                sstv_freq_slider = sstv_freq;
                prev_sstv_freq = sstv_freq;
            }
            if (node.HasValue("sstv_vol_slider"))
            {
                sstv_vol_slider = Single.Parse(node.GetValue("sstv_vol_slider"));
                prev_sstv_vol_slider = sstv_vol_slider;
            }
            if (node.HasValue("sel_beep_src")) sel_beep_src = Int32.Parse(node.GetValue("sel_beep_src"));
            if (sel_beep_src < 0 || sel_beep_src > 9) sel_beep_src = 0;
            if (node.HasValue("sel_beep_page")) sel_beep_page = Int32.Parse(node.GetValue("sel_beep_page"));

            //AAE
            int i;

            if (aae_backgrounds_exist)
            {
                i = 0;

                foreach (ConfigNode _background in node.nodes)
                {
                    if (_background.name == "AAE_BACKGROUND")
                    {
                        add_new_backgroundsource();
                        if (_background.HasValue("volume")) backgroundsource_list[i].audiosource.volume = Single.Parse(_background.GetValue("volume"));
                        if (_background.HasValue("current_clip")) backgroundsource_list[i].current_clip = _background.GetValue("current_clip");

                        if (dict_background_samples.Count > 0)
                        {
                            set_background_clip(backgroundsource_list[i]);
                        }
                        i++;
                    }
                }
            }

            if (aae_soundscapes_exist)
            {
                if (node.HasValue("aae_soundscape_vol")) aae_soundscape.volume = Single.Parse(node.GetValue("aae_soundscape_vol"));
                if (node.HasValue("aae_soundscape_freq"))
                {
                    aae_soundscape_freq = Int32.Parse(node.GetValue("aae_soundscape_freq"));
                    aae_prev_soundscape_freq = aae_soundscape_freq;
                }
            }

            if (aae_breathing_exist)
            {
                if (node.HasValue("aae_breathing_vol")) aae_breathing.volume = Single.Parse(node.GetValue("aae_breathing_vol"));
            }

            if (aae_airlock_exist)
            {
                if (node.HasValue("aae_airlock_vol")) aae_airlock.volume = Single.Parse(node.GetValue("aae_airlock_vol"));
            }

            if (aae_wind_exist)
            {
                if (node.HasValue("aae_wind_vol"))
                {
                    aae_wind_vol_slider = Single.Parse(node.GetValue("aae_wind_vol"));
                    aae_wind.volume = aae_wind_vol_slider;
                }
            }

            //
            //Load audioset info
            i = 0;
            foreach (ConfigNode _set in node.nodes)
            {
                if (_set.name == "AUDIOSET")
                {
                    chatter_array.Add(new ChatterAudioList());  //create a new entry in the list for each audioset
                    if (_set.HasValue("directory")) chatter_array[i].directory = _set.GetValue("directory");
                    if (_set.HasValue("is_active")) chatter_array[i].is_active = Boolean.Parse(_set.GetValue("is_active"));
                    i++;
                }
            }
            if (debugging) Debug.Log("[CHATR] audiosets found: " + chatter_array.Count + " :: reloading chatter audio");
            load_chatter_audio();   //reload audio

            //Chatter filters
            foreach (ConfigNode _filter in node.nodes)
            {
                if (_filter.name == "CHORUS")
                {
                    if (_filter.HasValue("enabled"))
                    {
                        chatter_chorus_filter.enabled = Boolean.Parse(_filter.GetValue("enabled"));
                        //if (debugging) Debug.Log("[CHATR] beepsource index: " + x.ToString() + " :: chorus_enabled = " + beepsource_list[x].chorus_filter.enabled);
                    }
                    if (_filter.HasValue("dry_mix")) chatter_chorus_filter.dryMix = Single.Parse(_filter.GetValue("dry_mix"));
                    if (_filter.HasValue("wet_mix_1")) chatter_chorus_filter.wetMix1 = Single.Parse(_filter.GetValue("wet_mix_1"));
                    if (_filter.HasValue("wet_mix_2")) chatter_chorus_filter.wetMix2 = Single.Parse(_filter.GetValue("wet_mix_2"));
                    if (_filter.HasValue("wet_mix_3")) chatter_chorus_filter.wetMix3 = Single.Parse(_filter.GetValue("wet_mix_3"));
                }
                else if (_filter.name == "DISTORTION")
                {
                    if (_filter.HasValue("enabled")) chatter_distortion_filter.enabled = Boolean.Parse(_filter.GetValue("enabled"));
                    if (_filter.HasValue("distortion_level")) chatter_distortion_filter.distortionLevel = Single.Parse(_filter.GetValue("distortion_level"));
                }
                else if (_filter.name == "ECHO")
                {
                    if (_filter.HasValue("enabled")) chatter_echo_filter.enabled = Boolean.Parse(_filter.GetValue("enabled"));
                    if (_filter.HasValue("delay")) chatter_echo_filter.delay = Single.Parse(_filter.GetValue("delay"));
                    if (_filter.HasValue("decay_ratio")) chatter_echo_filter.decayRatio = Single.Parse(_filter.GetValue("decay_ratio"));
                    if (_filter.HasValue("dry_mix")) chatter_echo_filter.dryMix = Single.Parse(_filter.GetValue("dry_mix"));
                    if (_filter.HasValue("wet_mix")) chatter_echo_filter.wetMix = Single.Parse(_filter.GetValue("wet_mix"));
                }
                else if (_filter.name == "HIGHPASS")
                {
                    if (_filter.HasValue("enabled")) chatter_highpass_filter.enabled = Boolean.Parse(_filter.GetValue("enabled"));
                    if (_filter.HasValue("cutoff_freq")) chatter_highpass_filter.cutoffFrequency = Single.Parse(_filter.GetValue("cutoff_freq"));
                    if (_filter.HasValue("resonance_q")) chatter_highpass_filter.highpassResonaceQ = Single.Parse(_filter.GetValue("resonance_q"));
                }
                else if (_filter.name == "LOWPASS")
                {
                    if (_filter.HasValue("enabled")) chatter_lowpass_filter.enabled = Boolean.Parse(_filter.GetValue("enabled"));
                    if (_filter.HasValue("cutoff_freq")) chatter_lowpass_filter.cutoffFrequency = Single.Parse(_filter.GetValue("cutoff_freq"));
                    if (_filter.HasValue("resonance_q")) chatter_lowpass_filter.lowpassResonaceQ = Single.Parse(_filter.GetValue("resonance_q"));
                }
                else if (_filter.name == "REVERB")
                {
                    if (_filter.HasValue("enabled")) chatter_reverb_filter.enabled = Boolean.Parse(_filter.GetValue("enabled"));
                    if (_filter.HasValue("reverb_preset")) chatter_reverb_filter.reverbPreset = (AudioReverbPreset)Enum.Parse(typeof(AudioReverbPreset), _filter.GetValue("reverb_preset"));
                    if (_filter.HasValue("dry_level")) chatter_reverb_filter.dryLevel = Single.Parse(_filter.GetValue("dry_level"));
                    if (_filter.HasValue("room")) chatter_reverb_filter.room = Single.Parse(_filter.GetValue("room"));
                    if (_filter.HasValue("room_hf")) chatter_reverb_filter.roomHF = Single.Parse(_filter.GetValue("room_hf"));
                    if (_filter.HasValue("room_lf")) chatter_reverb_filter.roomLF = Single.Parse(_filter.GetValue("room_lf"));
                    if (_filter.HasValue("room_rolloff")) chatter_reverb_filter.roomRolloff = Single.Parse(_filter.GetValue("room_rolloff"));
                    if (_filter.HasValue("decay_time")) chatter_reverb_filter.decayTime = Single.Parse(_filter.GetValue("decay_time"));
                    if (_filter.HasValue("decay_hf_ratio")) chatter_reverb_filter.decayHFRatio = Single.Parse(_filter.GetValue("decay_hf_ratio"));
                    if (_filter.HasValue("reflections_level")) chatter_reverb_filter.reflectionsLevel = Single.Parse(_filter.GetValue("reflections_level"));
                    if (_filter.HasValue("reflections_delay")) chatter_reverb_filter.reflectionsDelay = Single.Parse(_filter.GetValue("reflections_delay"));
                    if (_filter.HasValue("reverb_level")) chatter_reverb_filter.reverbLevel = Single.Parse(_filter.GetValue("reverb_level"));
                    if (_filter.HasValue("reverb_delay")) chatter_reverb_filter.reverbDelay = Single.Parse(_filter.GetValue("reverb_delay"));
                    if (_filter.HasValue("diffusion")) chatter_reverb_filter.diffusion = Single.Parse(_filter.GetValue("diffusion"));
                    if (_filter.HasValue("density")) chatter_reverb_filter.density = Single.Parse(_filter.GetValue("density"));
                    if (_filter.HasValue("hf_reference")) chatter_reverb_filter.hfReference = Single.Parse(_filter.GetValue("hf_reference"));
                    if (_filter.HasValue("lf_reference")) chatter_reverb_filter.lFReference = Single.Parse(_filter.GetValue("lf_reference"));
                }
            }

            //Beepsources
            foreach (ConfigNode _source in node.nodes)
            {
                if (_source.name == "BEEPSOURCE")
                {
                    if (debugging) Debug.Log("[CHATR] loading beepsource");
                    add_new_beepsource();

                    int x = beepsource_list.Count - 1;

                    if (_source.HasValue("precise")) beepsource_list[x].precise = Boolean.Parse(_source.GetValue("precise"));
                    if (_source.HasValue("precise_freq"))
                    {
                        beepsource_list[x].precise_freq = Int32.Parse(_source.GetValue("precise_freq"));
                        beepsource_list[x].precise_freq_slider = beepsource_list[x].precise_freq;
                    }
                    if (_source.HasValue("loose_freq"))
                    {
                        beepsource_list[x].loose_freq = Int32.Parse(_source.GetValue("loose_freq"));
                        beepsource_list[x].loose_freq_slider = beepsource_list[x].loose_freq;
                    }
                    if (_source.HasValue("volume")) beepsource_list[x].audiosource.volume = Single.Parse(_source.GetValue("volume"));
                    if (_source.HasValue("pitch")) beepsource_list[x].audiosource.pitch = Single.Parse(_source.GetValue("pitch"));
                    if (_source.HasValue("current_clip")) beepsource_list[x].current_clip = _source.GetValue("current_clip");
                    if (_source.HasValue("sel_filter")) beepsource_list[x].sel_filter = Int32.Parse(_source.GetValue("sel_filter"));
                    if (_source.HasValue("show_settings_window")) beepsource_list[x].show_settings_window = Boolean.Parse(_source.GetValue("show_settings_window"));
                    if (_source.HasValue("reverb_preset_index")) beepsource_list[x].reverb_preset_index = Int32.Parse(_source.GetValue("reverb_preset_index"));
                    if (_source.HasValue("settings_window_pos_x")) beepsource_list[x].settings_window_pos.x = Single.Parse(_source.GetValue("settings_window_pos_x"));
                    if (_source.HasValue("settings_window_pos_y")) beepsource_list[x].settings_window_pos.y = Single.Parse(_source.GetValue("settings_window_pos_y"));

                    if (dict_probe_samples.Count > 0)
                    {
                        set_beep_clip(beepsource_list[x]);

                        if (beepsource_list[x].precise == false && beepsource_list[x].loose_freq > 0) new_beep_loose_timer_limit(beepsource_list[x]);
                    }

                    foreach (ConfigNode _filter in _source.nodes)
                    {
                        if (_filter.name == "CHORUS")
                        {
                            if (_filter.HasValue("enabled"))
                            {
                                beepsource_list[x].chorus_filter.enabled = Boolean.Parse(_filter.GetValue("enabled"));
                                //if (debugging) Debug.Log("[CHATR] beepsource index: " + x.ToString() + " :: chorus_enabled = " + beepsource_list[x].chorus_filter.enabled);
                            }
                            if (_filter.HasValue("dry_mix")) beepsource_list[x].chorus_filter.dryMix = Single.Parse(_filter.GetValue("dry_mix"));
                            if (_filter.HasValue("wet_mix_1")) beepsource_list[x].chorus_filter.wetMix1 = Single.Parse(_filter.GetValue("wet_mix_1"));
                            if (_filter.HasValue("wet_mix_2")) beepsource_list[x].chorus_filter.wetMix2 = Single.Parse(_filter.GetValue("wet_mix_2"));
                            if (_filter.HasValue("wet_mix_3")) beepsource_list[x].chorus_filter.wetMix3 = Single.Parse(_filter.GetValue("wet_mix_3"));
                        }
                        else if (_filter.name == "DISTORTION")
                        {
                            if (_filter.HasValue("enabled")) beepsource_list[x].distortion_filter.enabled = Boolean.Parse(_filter.GetValue("enabled"));
                            if (_filter.HasValue("distortion_level")) beepsource_list[x].distortion_filter.distortionLevel = Single.Parse(_filter.GetValue("distortion_level"));
                        }
                        else if (_filter.name == "ECHO")
                        {
                            if (_filter.HasValue("enabled")) beepsource_list[x].echo_filter.enabled = Boolean.Parse(_filter.GetValue("enabled"));
                            if (_filter.HasValue("delay")) beepsource_list[x].echo_filter.delay = Single.Parse(_filter.GetValue("delay"));
                            if (_filter.HasValue("decay_ratio")) beepsource_list[x].echo_filter.decayRatio = Single.Parse(_filter.GetValue("decay_ratio"));
                            if (_filter.HasValue("dry_mix")) beepsource_list[x].echo_filter.dryMix = Single.Parse(_filter.GetValue("dry_mix"));
                            if (_filter.HasValue("wet_mix")) beepsource_list[x].echo_filter.wetMix = Single.Parse(_filter.GetValue("wet_mix"));
                        }
                        else if (_filter.name == "HIGHPASS")
                        {
                            if (_filter.HasValue("enabled")) beepsource_list[x].highpass_filter.enabled = Boolean.Parse(_filter.GetValue("enabled"));
                            if (_filter.HasValue("cutoff_freq")) beepsource_list[x].highpass_filter.cutoffFrequency = Single.Parse(_filter.GetValue("cutoff_freq"));
                            if (_filter.HasValue("resonance_q")) beepsource_list[x].highpass_filter.highpassResonaceQ = Single.Parse(_filter.GetValue("resonance_q"));
                        }
                        else if (_filter.name == "LOWPASS")
                        {
                            if (_filter.HasValue("enabled")) beepsource_list[x].lowpass_filter.enabled = Boolean.Parse(_filter.GetValue("enabled"));
                            if (_filter.HasValue("cutoff_freq")) beepsource_list[x].lowpass_filter.cutoffFrequency = Single.Parse(_filter.GetValue("cutoff_freq"));
                            if (_filter.HasValue("resonance_q")) beepsource_list[x].lowpass_filter.lowpassResonaceQ = Single.Parse(_filter.GetValue("resonance_q"));
                        }
                        else if (_filter.name == "REVERB")
                        {
                            if (_filter.HasValue("enabled")) beepsource_list[x].reverb_filter.enabled = Boolean.Parse(_filter.GetValue("enabled"));
                            if (_filter.HasValue("reverb_preset")) beepsource_list[x].reverb_filter.reverbPreset = (AudioReverbPreset)Enum.Parse(typeof(AudioReverbPreset), _filter.GetValue("reverb_preset"));
                            if (_filter.HasValue("dry_level")) beepsource_list[x].reverb_filter.dryLevel = Single.Parse(_filter.GetValue("dry_level"));
                            if (_filter.HasValue("room")) beepsource_list[x].reverb_filter.room = Single.Parse(_filter.GetValue("room"));
                            if (_filter.HasValue("room_hf")) beepsource_list[x].reverb_filter.roomHF = Single.Parse(_filter.GetValue("room_hf"));
                            if (_filter.HasValue("room_lf")) beepsource_list[x].reverb_filter.roomLF = Single.Parse(_filter.GetValue("room_lf"));
                            if (_filter.HasValue("room_rolloff")) beepsource_list[x].reverb_filter.roomRolloff = Single.Parse(_filter.GetValue("room_rolloff"));
                            if (_filter.HasValue("decay_time")) beepsource_list[x].reverb_filter.decayTime = Single.Parse(_filter.GetValue("decay_time"));
                            if (_filter.HasValue("decay_hf_ratio")) beepsource_list[x].reverb_filter.decayHFRatio = Single.Parse(_filter.GetValue("decay_hf_ratio"));
                            if (_filter.HasValue("reflections_level")) beepsource_list[x].reverb_filter.reflectionsLevel = Single.Parse(_filter.GetValue("reflections_level"));
                            if (_filter.HasValue("reflections_delay")) beepsource_list[x].reverb_filter.reflectionsDelay = Single.Parse(_filter.GetValue("reflections_delay"));
                            if (_filter.HasValue("reverb_level")) beepsource_list[x].reverb_filter.reverbLevel = Single.Parse(_filter.GetValue("reverb_level"));
                            if (_filter.HasValue("reverb_delay")) beepsource_list[x].reverb_filter.reverbDelay = Single.Parse(_filter.GetValue("reverb_delay"));
                            if (_filter.HasValue("diffusion")) beepsource_list[x].reverb_filter.diffusion = Single.Parse(_filter.GetValue("diffusion"));
                            if (_filter.HasValue("density")) beepsource_list[x].reverb_filter.density = Single.Parse(_filter.GetValue("density"));
                            if (_filter.HasValue("hf_reference")) beepsource_list[x].reverb_filter.hfReference = Single.Parse(_filter.GetValue("hf_reference"));
                            if (_filter.HasValue("lf_reference")) beepsource_list[x].reverb_filter.lFReference = Single.Parse(_filter.GetValue("lf_reference"));
                        }
                    }
                }
            }
            if (debugging) Debug.Log("[CHATR] load_shared_settings() END");
        }

        ////Check for a newer version
        //private void get_latest_version()
        //{
        //    bool got_all_info = false;

        //    WWWForm form = new WWWForm();
        //    form.AddField("version", this_version);

        //    WWW version = new WWW("http://rbri.co.nf/ksp/chatterer/get_latest_version.php", form.data);

        //    while (got_all_info == false)
        //    {
        //        if (version.isDone)
        //        {
        //            latest_version = version.text;
        //            got_all_info = true;
        //        }
        //    }
        //    recvd_latest_version = true;
        //    if (debugging) Debug.Log("[CHATR] recv'd latest version info: " + latest_version);
        //}

        //determine whether the vessel has a part with ModuleRemoteTechSPU and load all relevant RemoteTech variables for the vessel
        public void updateRemoteTechData()
        {
            //iterate through all vessel parts and look for a part containing ModuleRemoteTechSPU
            foreach (Part p in vessel.parts)
            {
                if (p.Modules.Contains("ModuleRemoteTechSPU"))
                {
                    //create BaseEventData field
                    BaseEventData data = new BaseEventData(BaseEventData.Sender.USER);

                    //load data into the BaseEventData field using the RTinterface KSPEvent of ModuleRemoteTechSPU.
                    p.Modules["ModuleRemoteTechSPU"].Events["RTinterface"].Invoke(data);

                    //ModuleRemoteTechSPU was found, so the vessel has RemoteTech
                    hasRemoteTech = true;

                    //cache the loaded data to local fields.
                    attitudeActive = data.Get<bool>("attitudeActive");
                    //localControl = data.Get<bool>("localControl");
                    inRadioContact = data.Get<bool>("inRadioContact");
                    controlDelay = data.Get<double>("controlDelay");

                    //end iteration and method
                    return;
                }

                //if iteration didn't find any ModuleRemoteTechSPU the vessel doesn't have RemoteTech
                hasRemoteTech = false;
                inRadioContact = false;
                controlDelay = 0;
            }
        }

        //Load audio functions
        private void load_quindar_audio()
        {
            //Create two AudioSources for quindar so PlayDelayed() can delay both beeps
            if (debugging) Debug.Log("[CHATR] loading Quindar clip");
            string path = "Chatterer/Sounds/chatter/quindar_01";

            if (GameDatabase.Instance.ExistsAudioClip(path))
            {
                quindar_clip = GameDatabase.Instance.GetAudioClip(path);
                if (debugging) Debug.Log("CHATR] Quindar clip loaded");
            }
            else Debug.LogWarning("[CHATR] Quindar audio file missing!");
        }

        private void load_beep_audio()
        {
            string[] audio_file_ext = { "*.wav", "*.ogg", "*.aif", "*.aiff" };

            string probe_sounds_root = AssemblyLoader.loadedAssemblies.GetPathByType(typeof(chatterer)) + "/../../../Sounds/beeps/";
            //if (Application.platform == RuntimePlatform.OSXPlayer) probe_sounds_root = KSPUtil.ApplicationRootPath.Substring(0, KSPUtil.ApplicationRootPath.IndexOf("KSP.app")) + "GameData/RBR/Sounds/probe/";

            if (Directory.Exists(probe_sounds_root))
            {
                beeps_exists = true;

                string[] st_array;
                foreach (string ext in audio_file_ext)
                {
                    //if (debugging) Debug.Log("[CHATR] checking for " + ext + " files...");
                    st_array = Directory.GetFiles(probe_sounds_root, ext);
                    foreach (string file in st_array)
                    {
                        //if (debugging) Debug.Log("[CHATR] probe file = " + file);
                        //[CHATR] file = C:/KSP/ksp-win-0-21-1/KSP_win/GameData/RBR/Sounds/apollo11/capcom/capcom_16.ogg

                        //tear out the whole root + directory + st + one more for final slash
                        int start_pos = probe_sounds_root.Length;
                        string file_name = file.Substring(start_pos);
                        //end pos to find the pos of the "."
                        int end_pos = file_name.LastIndexOf(".");
                        //now need a length between 

                        string short_file_name = file_name.Substring(0, end_pos);

                        //if (debugging) Debug.Log("[CHATR] file_name = " + file_name);

                        if (ext == "*.mp3")
                        {
                            //GameDatabase won't load MP3
                            //try old method
                            string mp3_path = "file://" + AssemblyLoader.loadedAssemblies.GetPathByType(typeof(chatterer)) + "/../../../Sounds/beeps/" + short_file_name + ".mp3";
                            WWW www_chatter = new WWW(mp3_path);
                            if (www_chatter != null)
                            {
                                dict_probe_samples.Add(short_file_name, www_chatter.GetAudioClip(false));
                                dict_probe_samples2.Add(www_chatter.GetAudioClip(false), short_file_name);
                                if (debugging) Debug.Log("[CHATR] " + mp3_path + " loaded OK");
                            }
                            else
                            {
                                Debug.LogWarning("[CHATR] " + mp3_path + " load FAIL");
                            }
                        }
                        else
                        {
                            string gdb_path = "Chatterer/Sounds/beeps/" + short_file_name;
                            if (GameDatabase.Instance.ExistsAudioClip(gdb_path))
                            {
                                //all_beep_clips.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                                dict_probe_samples.Add(short_file_name, GameDatabase.Instance.GetAudioClip(gdb_path));
                                dict_probe_samples2.Add(GameDatabase.Instance.GetAudioClip(gdb_path), short_file_name);
                                //if (debugging) Debug.Log("[CHATR] " + gdb_path + " loaded OK");
                            }
                            else
                            {
                                //no ExistsAudioClip == false
                                Debug.LogWarning("[CHATR] " + gdb_path + " load FAIL");
                            }
                        }
                    }
                }
            }
        }

        private void load_sstv_audio()
        {
            string[] audio_file_ext = { "*.wav", "*.ogg", "*.aif", "*.aiff" };

            string sstv_sounds_root = AssemblyLoader.loadedAssemblies.GetPathByType(typeof(chatterer)) + "/../../../Sounds/sstv/";

            if (Directory.Exists(sstv_sounds_root))
            {
                sstv_exists = true;

                string[] st_array;
                foreach (string ext in audio_file_ext)
                {
                    //if (debugging) Debug.Log("[CHATR] checking for " + ext + " files...");
                    st_array = Directory.GetFiles(sstv_sounds_root, ext);
                    foreach (string file in st_array)
                    {
                        //if (debugging) Debug.Log("[CHATR] sstv file = " + file);
                        //[CHATR] file = C:/KSP/ksp-win-0-21-1/KSP_win/GameData/RBR/Sounds/apollo11/capcom/capcom_16.ogg

                        //tear out the whole root + directory + st + one more for final slash
                        int start_pos = sstv_sounds_root.Length;
                        string file_name = file.Substring(start_pos);
                        //end pos to find the pos of the "."
                        int end_pos = file_name.LastIndexOf(".");
                        //now need a length between 

                        string short_file_name = file_name.Substring(0, end_pos);

                        //if (debugging) Debug.Log("[CHATR] file_name = " + file_name);

                        if (ext == "*.mp3")
                        {
                            //GameDatabase won't load MP3
                            //try old method
                            string mp3_path = "file://" + AssemblyLoader.loadedAssemblies.GetPathByType(typeof(chatterer)) + "/../../../Sounds/sstv/" + short_file_name + ".mp3";
                            WWW www_chatter = new WWW(mp3_path);
                            if (www_chatter != null)
                            {
                                all_sstv_clips.Add(www_chatter.GetAudioClip(false));
                                if (debugging) Debug.Log("[CHATR] " + mp3_path + " loaded OK");
                            }
                            else
                            {
                                Debug.LogWarning("[CHATR] " + mp3_path + " load FAIL");
                            }
                        }
                        else
                        {
                            string gdb_path = "Chatterer/Sounds/sstv/" + short_file_name;
                            if (GameDatabase.Instance.ExistsAudioClip(gdb_path))
                            {
                                //all_beep_clips.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                                //dict_probe_samples.Add(short_file_name, GameDatabase.Instance.GetAudioClip(gdb_path));
                                //dict_probe_samples2.Add(GameDatabase.Instance.GetAudioClip(gdb_path), short_file_name);
                                all_sstv_clips.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                                //if (debugging) Debug.Log("[CHATR] " + gdb_path + " loaded OK");
                            }
                            else
                            {
                                //no ExistsAudioClip == false
                                Debug.LogWarning("[CHATR] " + gdb_path + " load FAIL, GameDatabase.Instance.ExistsAudioClip(" + short_file_name + ") == false");
                            }
                        }
                    }
                }
            }




            /*
            string path;
            int i;

            for (i = 1; i <= sstv_search_max; i++)
            {
                path = "RBR/Sounds/sstv_" + i.ToString("D2");
                if (GameDatabase.Instance.ExistsAudioClip(path))
                {
                    all_sstv_clips.Add(GameDatabase.Instance.GetAudioClip(path));
                    //print(path + " loaded OKAY");
                }
                //else
                //{
                //print(path + " load ERROR");
                //}
            }
            */
            if (all_sstv_clips.Count == 0) Debug.LogWarning("[CHATR] No SSTV clips found");
        }

        private void load_chatter_audio()
        {

            //first, start a loop through all the elements in chatter_array
            //check for a capsule directory
            //if exists, run GetFiles() for each of the file extensions


            string[] set_types = { "capcom", "capsule" };
            string[] audio_file_ext = { "*.wav", "*.ogg", "*.aif", "*.aiff" };
            int k;

            string chatter_root = AssemblyLoader.loadedAssemblies.GetPathByType(typeof(chatterer)) + "/../../../Sounds/chatter/";
            //string chatter_root = KSPUtil.ApplicationRootPath.Substring(0, KSPUtil.ApplicationRootPath.IndexOf("KSP_Data")) + "GameData/RBR/Sounds/chatter/";

            if (Directory.Exists(chatter_root))
            {
                chatter_exists = true;

                if (debugging) Debug.Log("[CHATR] loading chatter audio...");

                for (k = 0; k < chatter_array.Count; k++)
                {
                    if (Directory.Exists(chatter_root + chatter_array[k].directory))
                    {
                        //audioset directory found OK
                        //if (debugging) Debug.Log("[CHATR] directory [" + chatter_array[k].directory + "] found OK");
                        foreach (string st in set_types)
                        {
                            //search through each set_type (capcom, capsule)
                            if (Directory.Exists(chatter_root + chatter_array[k].directory + "/" + st))
                            {
                                //if (debugging) Debug.Log("[CHATR] directory [" + chatter_array[k].directory + "/" + st + "] found OK");

                                //if (debugging) Debug.Log("[CHATR] clearing existing " + chatter_array[k].directory + "/" + st + " audio");
                                if (st == "capcom") chatter_array[k].capcom.Clear();
                                else if (st == "capsule") chatter_array[k].capsule.Clear();  //clear any existing audio

                                string[] st_array;
                                foreach (string ext in audio_file_ext)
                                {
                                    //if (debugging) Debug.Log("[CHATR] checking for " + ext + " files...");
                                    st_array = Directory.GetFiles(chatter_root + chatter_array[k].directory + "/" + st + "/", ext);
                                    foreach (string file in st_array)
                                    {
                                        //if (debugging) Debug.Log("[CHATR] file = " + file);
                                        //[CHATR] file = C:/KSP/ksp-win-0-21-1/KSP_win/GameData/RBR/Sounds/apollo11/capcom\capcom_16.ogg
                                        //try it anyway
                                        //substring the capcom_16 out of file

                                        //tear out the whole root + directory + st + one more for final slash
                                        int start_pos = (chatter_root + chatter_array[k].directory + "/" + st + "/").Length;
                                        string file_name = file.Substring(start_pos);
                                        //end pos to find the pos of the "."
                                        int end_pos = file_name.LastIndexOf(".");
                                        //now need a length between 

                                        file_name = file_name.Substring(0, end_pos);

                                        //if (debugging) Debug.Log("[CHATR] file_name = " + file_name);

                                        if (ext == "*.mp3")
                                        {
                                            //try old method
                                            string mp3_path = "file://" + AssemblyLoader.loadedAssemblies.GetPathByType(typeof(chatterer)) + "/../../../Sounds/chatter/" + chatter_array[k].directory + "/" + st + "/" + file_name + ".mp3";
                                            WWW www_chatter = new WWW(mp3_path);
                                            if (www_chatter != null)
                                            {
                                                if (st == "capcom")
                                                {
                                                    chatter_array[k].capcom.Add(www_chatter.GetAudioClip(false));
                                                    //if (debugging) Debug.Log("[CHATR] " + mp3_path + " loaded OK");
                                                }
                                                else if (st == "capsule")
                                                {
                                                    chatter_array[k].capsule.Add(www_chatter.GetAudioClip(false));
                                                    //if (debugging) Debug.Log("[CHATR] " + mp3_path + " loaded OK");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            string gdb_path = "Chatterer/Sounds/chatter/" + chatter_array[k].directory + "/" + st + "/" + file_name;
                                            if (GameDatabase.Instance.ExistsAudioClip(gdb_path))
                                            {
                                                if (st == "capcom")
                                                {
                                                    chatter_array[k].capcom.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                                                    //if (debugging) Debug.Log("[CHATR] " + gdb_path + " loaded OK");
                                                }
                                                else if (st == "capsule")
                                                {
                                                    chatter_array[k].capsule.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                                                    //if (debugging) Debug.Log("[CHATR] " + gdb_path + " loaded OK");
                                                }
                                            }
                                            else
                                            {
                                                //no audio exists at gdb_path
                                                Debug.LogWarning("[CHATR] " + gdb_path + " load FAIL, trying old method");
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogWarning("[CHATR] directory [" + chatter_array[k].directory + "/" + st + "] NOT found, skipping...");
                            }
                        }
                    }
                    else
                    {
                        //audioset directory NOT found
                        Debug.LogWarning("[CHATR] directory [" + chatter_array[k].directory + "] NOT found, skipping...");
                    }
                }
            }

            load_toggled_chatter_sets();
        }

        private void load_AAE_background_audio()
        {
            string[] audio_file_ext = { "*.wav", "*.ogg", "*.aif", "*.aiff" };
            string sounds_path = AssemblyLoader.loadedAssemblies.GetPathByType(typeof(chatterer)) + "/../../../Sounds/AAE/background/";

            if (Directory.Exists(sounds_path))
            {
                //AAE_exists = true;  //set flag to display and run AAE functions if any AAE is found

                string[] st_array;
                foreach (string ext in audio_file_ext)
                {
                    //if (debugging) Debug.Log("[CHATR] checking for " + ext + " files...");
                    st_array = Directory.GetFiles(sounds_path, ext);
                    foreach (string file in st_array)
                    {
                        //get the file name without extension
                        int start_pos = sounds_path.Length;
                        string file_name = file.Substring(start_pos);
                        int end_pos = file_name.LastIndexOf(".");
                        file_name = file_name.Substring(0, end_pos);

                        string gdb_path = "Chatterer/Sounds/AAE/background/" + file_name;
                        if (GameDatabase.Instance.ExistsAudioClip(gdb_path))
                        {
                            aae_backgrounds_exist = true;
                            dict_background_samples.Add(file_name, GameDatabase.Instance.GetAudioClip(gdb_path));
                            dict_background_samples2.Add(GameDatabase.Instance.GetAudioClip(gdb_path), file_name);
                            //audio_list.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                            //if (debugging) Debug.Log("[muziker] " + gdb_path + " loaded OK");
                        }
                        else
                        {
                            //no ExistsAudioClip == false
                            Debug.LogWarning("[CHATR] Could not load audio " + gdb_path);
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("Directory '" + sounds_path + "' could not be found");
            }
        }

        private void load_AAE_soundscape_audio()
        {
            string[] audio_file_ext = { "*.wav", "*.ogg", "*.aif", "*.aiff" };
            string sounds_path = AssemblyLoader.loadedAssemblies.GetPathByType(typeof(chatterer)) + "/../../../Sounds/AAE/soundscape/";

            if (Directory.Exists(sounds_path))
            {
                //AAE_exists = true;  //set flag to display and run AAE functions if any AAE is found

                string[] st_array;
                foreach (string ext in audio_file_ext)
                {
                    //if (debugging) Debug.Log("[CHATR] checking for " + ext + " files...");
                    st_array = Directory.GetFiles(sounds_path, ext);
                    foreach (string file in st_array)
                    {
                        //get the file name without extension
                        int start_pos = sounds_path.Length;
                        string file_name = file.Substring(start_pos);
                        int end_pos = file_name.LastIndexOf(".");
                        file_name = file_name.Substring(0, end_pos);

                        string gdb_path = "Chatterer/Sounds/AAE/soundscape/" + file_name;
                        if (GameDatabase.Instance.ExistsAudioClip(gdb_path))
                        {
                            aae_soundscapes_exist = true;
                            dict_soundscape_samples.Add(file_name, GameDatabase.Instance.GetAudioClip(gdb_path));
                            dict_soundscape_samples2.Add(GameDatabase.Instance.GetAudioClip(gdb_path), file_name);
                            //audio_soundscape.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                            //audio_list.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                            //if (debugging) Debug.Log("[muziker] " + gdb_path + " loaded OK");
                        }
                        else
                        {
                            //no ExistsAudioClip == false
                            Debug.LogWarning("[CHATR] Could not load audio " + gdb_path);
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("Directory '" + sounds_path + "' could not be found");
            }
        }

        //Timer functions
        private void new_beep_loose_timer_limit(BeepSource bm)
        {
            if (bm.loose_freq == 1) bm.loose_timer_limit = rand.Next(120, 301);
            else if (bm.loose_freq == 2) bm.loose_timer_limit = rand.Next(60, 121);
            else if (bm.loose_freq == 3) bm.loose_timer_limit = rand.Next(30, 61);
            else if (bm.loose_freq == 4) bm.loose_timer_limit = rand.Next(15, 31);
            else if (bm.loose_freq == 5) bm.loose_timer_limit = rand.Next(5, 16);
            else if (bm.loose_freq == 6) bm.loose_timer_limit = rand.Next(1, 6);
            //if (debugging) Debug.Log("[CHATR] new beep loose timer limit set: " + bm.loose_timer_limit);
        }

        private void new_sstv_loose_timer_limit()
        {
            if (sstv_freq == 1) sstv_timer_limit = rand.Next(1800, 3601);       //30-60mins
            else if (sstv_freq == 2) sstv_timer_limit = rand.Next(900, 1801);   //15-30m
            else if (sstv_freq == 3) sstv_timer_limit = rand.Next(300, 901);    //5-15m
            else if (sstv_freq == 4) sstv_timer_limit = rand.Next(120, 301);    //2-5m
            if (debugging) Debug.Log("[CHATR] new sstv timer limit set: " + sstv_timer_limit.ToString("F0"));
        }

        private void new_soundscape_loose_timer_limit()
        {
            if (aae_soundscape_freq == 1) aae_soundscape_timer_limit = rand.Next(300, 601);   //5-10m
            if (aae_soundscape_freq == 2) aae_soundscape_timer_limit = rand.Next(120, 301);   //2-5m
            if (aae_soundscape_freq == 3) aae_soundscape_timer_limit = rand.Next(60, 121);   //1-2m
            if (debugging) Debug.Log("[CHATR] new soundscape1 timer limit set: " + aae_soundscape_timer_limit.ToString("F0"));
        }

        //Chatter functions
        private void load_toggled_chatter_sets()
        {
            //if (debugging) Debug.Log("[CHATR] loading toggled sets...");
            //load audio into current from sets that are toggled on
            current_capcom_chatter.Clear();
            current_capsule_chatter.Clear();

            int i;
            for (i = 0; i < chatter_array.Count; i++)
            {
                if (chatter_array[i].is_active == true)
                {
                    current_capcom_chatter.AddRange(chatter_array[i].capcom);
                    current_capsule_chatter.AddRange(chatter_array[i].capsule);
                }
            }

            //toggle has changed so resave audiosets.cfg
            //now done in write_settings()
            // i = 0;
            //foreach (ConfigNode _node in audiosets.nodes)
            //{
            //    _node.SetValue("is_active", chatter_array[i].is_active.ToString());
            //    i++;
            //}

            //audiosets.Save(audiosets_path);

            if (debugging) Debug.Log("[CHATR] toggled sets loaded OK");
        }

        private void set_new_delay_between_exchanges()
        {
            if (chatter_freq == 1) secs_between_exchanges = rand.Next(180, 301);
            else if (chatter_freq == 2) secs_between_exchanges = rand.Next(90, 181);
            else if (chatter_freq == 3) secs_between_exchanges = rand.Next(60, 91);
            else if (chatter_freq == 4) secs_between_exchanges = rand.Next(30, 61);
            else if (chatter_freq == 5) secs_between_exchanges = rand.Next(10, 31);
            if (debugging) Debug.Log("[CHATR] new delay between exchanges: " + secs_between_exchanges.ToString("F0"));
        }

        private void initialize_new_exchange()
        {
            //print("initialize_new_exchange()...");
            set_new_delay_between_exchanges();
            secs_since_last_exchange = 0;
            secs_since_initial_chatter = 0;
            current_capcom_clip = rand.Next(0, current_capcom_chatter.Count); // select a new capcom clip to play
            current_capsule_clip = rand.Next(0, current_capsule_chatter.Count); // select a new capsule clip to play
            response_delay_secs = rand.Next(2, 5);  // select another random int to set response delay time

            if (pod_begins_exchange) initial_chatter_source = 1;    //pod_begins_exchange set true OnUpdate when staging and on event change
            else initial_chatter_source = rand.Next(0, 2);   //if i_c_s == 0, con sends first message; if i_c_S == 1, pod sends first message

            if (initial_chatter_source == 0)
            {
                initial_chatter_set = current_capcom_chatter;
                response_chatter_set = current_capsule_chatter;
                initial_chatter_index = current_capcom_clip;
                response_chatter_index = current_capsule_clip;
            }
            else
            {
                initial_chatter_set = current_capsule_chatter;
                response_chatter_set = current_capcom_chatter;
                initial_chatter_index = current_capsule_clip;
                response_chatter_index = current_capcom_clip;
            }
            if (initial_chatter_set.Count > 0) initial_chatter.clip = initial_chatter_set[initial_chatter_index];
            else Debug.LogWarning("[CHATR] Initial chatter set is empty");
            if (response_chatter_set.Count > 0) response_chatter.clip = response_chatter_set[response_chatter_index];
            else Debug.LogWarning("[CHATR] Response chatter set is empty");
        }

        private void play_quindar(float delay)
        {
            //play quindar after initial delay
            //print("playing initial first quindar :: delay length = " + delay.ToString());
            quindar1.PlayDelayed(delay);
            // then play the initial chatter after a delay for quindar + initial delay
            //print("playing initial chatter :: delay length = " + (delay + quindar.clip.length).ToString());
            initial_chatter.PlayDelayed(delay + quindar1.clip.length);
            //replay quindar once more with initial delay, quindar delay, and initial chatter delay
            //print("playing initial second quindar :: delay length = " + (delay + quindar.clip.length + initial_chatter_set[initial_chatter_index].clip.length).ToString());
            quindar2.PlayDelayed(delay + quindar1.clip.length + initial_chatter.clip.length);
        }

        private void load_radio()
        {
            //try to load from disk first
            string path = Application.persistentDataPath +"radio2";

            //FIX this below will never return true since path isnt correct for GameDatabase
            
            //File.Exists instead or so
            
            if (GameDatabase.Instance.ExistsAudioClip(path))
            {
                yep_yepsource.clip = GameDatabase.Instance.GetAudioClip(path);
                yep_yep_loaded = true;
            }
            else
            {
                //try www download
                bool radio_loaded = false;
                WWW www_yepyep = new WWW("http://rbri.co.nf/ksp/chatterer/radio2.ogg");

                while (radio_loaded == false)
                {
                    if (www_yepyep.isDone)
                    {
                        yep_yepsource.clip = www_yepyep.GetAudioClip(false);
                        //SavWav.Save("radio2", yep_yepsource.clip);
                        if (debugging) Debug.Log("[CHATR] radio_yep_yep loaded OK");
                        radio_loaded = true;
                        yep_yep_loaded = true;
                    }
                }
            }
        }

        private void begin_exchange(float delay)
        {
            exchange_playing = true;
            initialize_new_exchange();

            if (initial_chatter_source == 1)
            {
                //capsule starts the exchange
                //Always play regardless of RT

                //play initial capsule chatter
                //initial_chatter_set[initial_chatter_index].PlayDelayed(delay);
                if (initial_chatter_set.Count > 0)
                {
                    initial_chatter.PlayDelayed(delay);
                }
                else
                {
                    exchange_playing = false;
                    Debug.LogWarning("[CHATR] initial_chatter_set has no audioclips, abandoning exchange");
                }

                //add RT delay to response delay if enabled and in contact
                if (remotetech_toggle && inRadioContact) response_delay_secs += Convert.ToInt32(controlDelay);

                //if RT is enabled but not in radio contact, play no response
                if (remotetech_toggle && inRadioContact == false) exchange_playing = false;
            }

            if (initial_chatter_source == 0)
            {
                //capcom starts the exchange
                if (remotetech_toggle == false)
                {
                    //RT is off
                    //always play initial capcom

                    if (initial_chatter_set.Count > 0)
                    {
                        if (quindar_toggle)
                        {
                            play_quindar(delay);
                        }
                        else initial_chatter.PlayDelayed(delay); // play without quindar

                        //initial_chatter.PlayDelayed(delay);
                    }
                    else
                    {
                        exchange_playing = false;
                        Debug.LogWarning("[CHATR] initial_chatter_set has no audioclips, abandoning exchange");
                    }
                }
                if (remotetech_toggle && hasRemoteTech && inRadioContact)
                {
                    //RT is on and in radio contact
                    //play initial capcom
                    delay += Convert.ToSingle(controlDelay);    //add RT delay to any current delay
                    if (initial_chatter_set.Count > 0)
                    {
                        //initial_chatter.PlayDelayed(delay);
                        if (quindar_toggle) play_quindar(delay);    // play with quindar
                        else initial_chatter.PlayDelayed(delay); // play without quindar
                    }
                    else
                    {
                        exchange_playing = false;
                        Debug.LogWarning("[CHATR] initial_chatter_set has no audioclips, abandoning exchange");
                    }
                }
                if (remotetech_toggle && inRadioContact == false)
                {
                    //RT is on but not in radio contact
                    //play no initial chatter or response
                    exchange_playing = false;
                }
            }
        }

        private void stop_audio(string audio_type)
        {
            if (audio_type == "all")
            {
                foreach (BeepSource bm in beepsource_list)
                {
                    bm.audiosource.Stop();
                    bm.timer = 0;
                }
                initial_chatter.Stop();
                response_chatter.Stop();
                quindar1.Stop();
                quindar2.Stop();
                exchange_playing = false;
            }
            else if (audio_type == "beeps")
            {
                foreach (BeepSource bm in beepsource_list)
                {
                    bm.audiosource.loop = false;
                    bm.audiosource.Stop();
                    bm.timer = 0;
                }
            }
        }

        //Create filter defaults to use when reseting filters
        private void create_filter_defaults_node()
        {
            filter_defaults = new ConfigNode();
            filter_defaults.name = "FILTERS";

            ConfigNode _filter;

            _filter = new ConfigNode();
            _filter.name = "CHORUS";
            //_filter.AddValue("enabled", false);
            _filter.AddValue("dry_mix", 0.5f);
            _filter.AddValue("wet_mix_1", 0.5f);
            _filter.AddValue("wet_mix_2", 0.5f);
            _filter.AddValue("wet_mix_3", 0.5f);
            _filter.AddValue("delay", 40.0f);
            _filter.AddValue("rate", 0.8f);
            _filter.AddValue("depth", 0.03f);
            filter_defaults.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "DISTORTION";
            //_filter.AddValue("enabled", false);
            _filter.AddValue("distortion_level", 0.5f);
            filter_defaults.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "ECHO";
            //_filter.AddValue("enabled", false);
            _filter.AddValue("delay", 500.0f);
            _filter.AddValue("decay_ratio", 0.5f);
            _filter.AddValue("dry_mix", 1.0f);
            _filter.AddValue("wet_mix", 1.0f);
            filter_defaults.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "HIGHPASS";
            //_filter.AddValue("enabled", false);
            _filter.AddValue("cutoff_freq", 5000.0f);
            //_filter.AddValue("resonance_q", "");  //TODO default highpass resonance q missing from Unity Doc webpage.  figure it out
            filter_defaults.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "LOWPASS";
            //_filter.AddValue("enabled", false);
            _filter.AddValue("cutoff_freq", 5000.0f);
            //_filter.AddValue("resonance_q", "");  //TODO default lowpass resonance q missing from Unity Doc webpage.  figure it out
            filter_defaults.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "REVERB";
            //_filter.AddValue("enabled", false);
            _filter.AddValue("reverb_preset", AudioReverbPreset.User);
            _filter.AddValue("dry_level", 0);
            _filter.AddValue("room", 0);
            _filter.AddValue("room_hf", 0);
            _filter.AddValue("room_lf", 0);
            _filter.AddValue("room_rolloff", 10.0f);
            _filter.AddValue("decay_time", 1.0f);
            _filter.AddValue("decay_hf_ratio", 0.5f);
            _filter.AddValue("reflections_level", -10000.0f);
            _filter.AddValue("reflections_delay", 0);
            _filter.AddValue("reverb_level", 0);
            _filter.AddValue("reverb_delay", 0.04f);
            _filter.AddValue("diffusion", 100.0f);
            _filter.AddValue("density", 100.0f);
            _filter.AddValue("hf_reference", 5000.0f);
            _filter.AddValue("lf_reference", 250.0f);
            filter_defaults.AddNode(_filter);
        }

        //Copy/Paste beepsource
        private void copy_beepsource_values(BeepSource source)
        {
            beepsource_clipboard = new ConfigNode();

            ConfigNode _filter;

            beepsource_clipboard.AddValue("precise", source.precise);
            beepsource_clipboard.AddValue("precise_freq", source.precise_freq);
            beepsource_clipboard.AddValue("loose_freq", source.loose_freq);
            beepsource_clipboard.AddValue("volume", source.audiosource.volume);
            beepsource_clipboard.AddValue("pitch", source.audiosource.pitch);
            beepsource_clipboard.AddValue("current_clip", source.current_clip);
            beepsource_clipboard.AddValue("sel_filter", source.sel_filter);
            beepsource_clipboard.AddValue("show_settings_window", source.show_settings_window);
            beepsource_clipboard.AddValue("reverb_preset_index", source.reverb_preset_index);
            beepsource_clipboard.AddValue("settings_window_pos_x", source.settings_window_pos.x);
            beepsource_clipboard.AddValue("settings_window_pos_y", source.settings_window_pos.y);

            //filters
            //ConfigNode _filter;

            _filter = new ConfigNode();
            _filter.name = "CHORUS";
            _filter.AddValue("enabled", source.chorus_filter.enabled);
            _filter.AddValue("dry_mix", source.chorus_filter.dryMix);
            _filter.AddValue("wet_mix_1", source.chorus_filter.wetMix1);
            _filter.AddValue("wet_mix_2", source.chorus_filter.wetMix2);
            _filter.AddValue("wet_mix_3", source.chorus_filter.wetMix3);
            beepsource_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "DISTORTION";
            _filter.AddValue("enabled", source.distortion_filter.enabled);
            _filter.AddValue("distortion_level", source.distortion_filter.distortionLevel);
            beepsource_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "ECHO";
            _filter.AddValue("enabled", source.echo_filter.enabled);
            _filter.AddValue("delay", source.echo_filter.delay);
            _filter.AddValue("decay_ratio", source.echo_filter.decayRatio);
            _filter.AddValue("dry_mix", source.echo_filter.dryMix);
            _filter.AddValue("wet_mix", source.echo_filter.wetMix);
            beepsource_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "HIGHPASS";
            _filter.AddValue("enabled", source.highpass_filter.enabled);
            _filter.AddValue("cutoff_freq", source.highpass_filter.cutoffFrequency);
            _filter.AddValue("resonance_q", source.highpass_filter.highpassResonaceQ);
            beepsource_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "LOWPASS";
            _filter.AddValue("enabled", source.lowpass_filter.enabled);
            _filter.AddValue("cutoff_freq", source.lowpass_filter.cutoffFrequency);
            _filter.AddValue("resonance_q", source.lowpass_filter.lowpassResonaceQ);
            beepsource_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "REVERB";
            _filter.AddValue("enabled", source.reverb_filter.enabled);
            _filter.AddValue("reverb_preset", source.reverb_filter.reverbPreset);
            _filter.AddValue("dry_level", source.reverb_filter.dryLevel);
            _filter.AddValue("room", source.reverb_filter.room);
            _filter.AddValue("room_hf", source.reverb_filter.roomHF);
            _filter.AddValue("room_lf", source.reverb_filter.roomLF);
            _filter.AddValue("room_rolloff", source.reverb_filter.roomRolloff);
            _filter.AddValue("decay_time", source.reverb_filter.decayTime);
            _filter.AddValue("decay_hf_ratio", source.reverb_filter.decayHFRatio);
            _filter.AddValue("reflections_level", source.reverb_filter.reflectionsLevel);
            _filter.AddValue("reflections_delay", source.reverb_filter.reflectionsDelay);
            _filter.AddValue("reverb_level", source.reverb_filter.reverbLevel);
            _filter.AddValue("reverb_delay", source.reverb_filter.reverbDelay);
            _filter.AddValue("diffusion", source.reverb_filter.diffusion);
            _filter.AddValue("density", source.reverb_filter.density);
            _filter.AddValue("hf_reference", source.reverb_filter.hfReference);
            _filter.AddValue("lf_reference", source.reverb_filter.lFReference);
            beepsource_clipboard.AddNode(_filter);

            if (debugging) Debug.Log("[CHATR] single beepsource values copied to beepsource_clipboard");
        }

        private void paste_beepsource_values(BeepSource source)
        {
            if (beepsource_clipboard.HasValue("precise")) source.precise = Boolean.Parse(beepsource_clipboard.GetValue("precise"));
            if (beepsource_clipboard.HasValue("precise_freq"))
            {
                source.precise_freq = Int32.Parse(beepsource_clipboard.GetValue("precise_freq"));
                source.precise_freq_slider = source.precise_freq;
            }
            if (beepsource_clipboard.HasValue("loose_freq"))
            {
                source.loose_freq = Int32.Parse(beepsource_clipboard.GetValue("loose_freq"));
                source.loose_freq_slider = source.loose_freq;
            }
            if (beepsource_clipboard.HasValue("volume")) source.audiosource.volume = Single.Parse(beepsource_clipboard.GetValue("volume"));
            if (beepsource_clipboard.HasValue("pitch")) source.audiosource.pitch = Single.Parse(beepsource_clipboard.GetValue("pitch"));
            if (beepsource_clipboard.HasValue("current_clip")) source.current_clip = beepsource_clipboard.GetValue("current_clip");
            if (beepsource_clipboard.HasValue("sel_filter")) source.sel_filter = Int32.Parse(beepsource_clipboard.GetValue("sel_filter"));
            if (beepsource_clipboard.HasValue("show_settings_window")) source.show_settings_window = Boolean.Parse(beepsource_clipboard.GetValue("show_settings_window"));
            if (beepsource_clipboard.HasValue("reverb_preset_index")) source.reverb_preset_index = Int32.Parse(beepsource_clipboard.GetValue("reverb_preset_index"));
            if (beepsource_clipboard.HasValue("settings_window_pos_x")) source.settings_window_pos.x = Single.Parse(beepsource_clipboard.GetValue("settings_window_pos_x"));
            if (beepsource_clipboard.HasValue("settings_window_pos_y")) source.settings_window_pos.y = Single.Parse(beepsource_clipboard.GetValue("settings_window_pos_y"));

            if (dict_probe_samples.Count > 0)
            {
                set_beep_clip(source);

                if (source.precise == false && source.loose_freq > 0) new_beep_loose_timer_limit(source);
            }

            foreach (ConfigNode filter in beepsource_clipboard.nodes)
            {
                if (filter.name == "CHORUS")
                {
                    if (filter.HasValue("enabled")) source.chorus_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
                    if (filter.HasValue("dry_mix")) source.chorus_filter.dryMix = Single.Parse(filter.GetValue("dry_mix"));
                    if (filter.HasValue("wet_mix_1")) source.chorus_filter.wetMix1 = Single.Parse(filter.GetValue("wet_mix_1"));
                    if (filter.HasValue("wet_mix_2")) source.chorus_filter.wetMix2 = Single.Parse(filter.GetValue("wet_mix_2"));
                    if (filter.HasValue("wet_mix_3")) source.chorus_filter.wetMix3 = Single.Parse(filter.GetValue("wet_mix_3"));
                }
                else if (filter.name == "DISTORTION")
                {
                    if (filter.HasValue("enabled")) source.distortion_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
                    if (filter.HasValue("distortion_level")) source.distortion_filter.distortionLevel = Single.Parse(filter.GetValue("distortion_level"));
                }
                else if (filter.name == "ECHO")
                {
                    if (filter.HasValue("enabled")) source.echo_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
                    if (filter.HasValue("delay")) source.echo_filter.delay = Single.Parse(filter.GetValue("delay"));
                    if (filter.HasValue("decay_ratio")) source.echo_filter.decayRatio = Single.Parse(filter.GetValue("decay_ratio"));
                    if (filter.HasValue("dry_mix")) source.echo_filter.dryMix = Single.Parse(filter.GetValue("dry_mix"));
                    if (filter.HasValue("wet_mix")) source.echo_filter.wetMix = Single.Parse(filter.GetValue("wet_mix"));
                }
                else if (filter.name == "HIGHPASS")
                {
                    if (filter.HasValue("enabled")) source.highpass_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
                    if (filter.HasValue("cutoff_freq")) source.highpass_filter.cutoffFrequency = Single.Parse(filter.GetValue("cutoff_freq"));
                    if (filter.HasValue("resonance_q")) source.highpass_filter.highpassResonaceQ = Single.Parse(filter.GetValue("resonance_q"));
                }
                else if (filter.name == "LOWPASS")
                {
                    if (filter.HasValue("enabled")) source.lowpass_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
                    if (filter.HasValue("cutoff_freq")) source.lowpass_filter.cutoffFrequency = Single.Parse(filter.GetValue("cutoff_freq"));
                    if (filter.HasValue("resonance_q")) source.lowpass_filter.lowpassResonaceQ = Single.Parse(filter.GetValue("resonance_q"));
                }
                else if (filter.name == "REVERB")
                {
                    if (filter.HasValue("enabled")) source.reverb_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
                    if (filter.HasValue("reverb_preset")) source.reverb_filter.reverbPreset = (AudioReverbPreset)Enum.Parse(typeof(AudioReverbPreset), filter.GetValue("reverb_preset"));
                    if (filter.HasValue("dry_level")) source.reverb_filter.dryLevel = Single.Parse(filter.GetValue("dry_level"));
                    if (filter.HasValue("room")) source.reverb_filter.room = Single.Parse(filter.GetValue("room"));
                    if (filter.HasValue("room_hf")) source.reverb_filter.roomHF = Single.Parse(filter.GetValue("room_hf"));
                    if (filter.HasValue("room_lf")) source.reverb_filter.roomLF = Single.Parse(filter.GetValue("room_lf"));
                    if (filter.HasValue("room_rolloff")) source.reverb_filter.roomRolloff = Single.Parse(filter.GetValue("room_rolloff"));
                    if (filter.HasValue("decay_time")) source.reverb_filter.decayTime = Single.Parse(filter.GetValue("decay_time"));
                    if (filter.HasValue("decay_hf_ratio")) source.reverb_filter.decayHFRatio = Single.Parse(filter.GetValue("decay_hf_ratio"));
                    if (filter.HasValue("reflections_level")) source.reverb_filter.reflectionsLevel = Single.Parse(filter.GetValue("reflections_level"));
                    if (filter.HasValue("reflections_delay")) source.reverb_filter.reflectionsDelay = Single.Parse(filter.GetValue("reflections_delay"));
                    if (filter.HasValue("reverb_level")) source.reverb_filter.reverbLevel = Single.Parse(filter.GetValue("reverb_level"));
                    if (filter.HasValue("reverb_delay")) source.reverb_filter.reverbDelay = Single.Parse(filter.GetValue("reverb_delay"));
                    if (filter.HasValue("diffusion")) source.reverb_filter.diffusion = Single.Parse(filter.GetValue("diffusion"));
                    if (filter.HasValue("density")) source.reverb_filter.density = Single.Parse(filter.GetValue("density"));
                    if (filter.HasValue("hf_reference")) source.reverb_filter.hfReference = Single.Parse(filter.GetValue("hf_reference"));
                    if (filter.HasValue("lf_reference")) source.reverb_filter.lFReference = Single.Parse(filter.GetValue("lf_reference"));
                }
            }
            if (debugging) Debug.Log("[CHATR] single beepsource values pasted from beepsource_clipboard");
        }

        //Functions for per-vessel settings
        private void new_vessel_node(Vessel v)
        {
            //if (debugging) Debug.Log("[CHATR] new_vessel_node() START");
            ConfigNode vessel_node = new ConfigNode();

            //cn_vessel.name = v.id.ToString();
            //temp_vessels_node = new ConfigNode();
            vessel_node.name = "VESSEL";

            vessel_node.AddValue("vessel_name", v.vesselName);
            vessel_node.AddValue("vessel_id", v.id.ToString());

            save_shared_settings(vessel_node);


            /*
            //
            vessel_node.AddValue("main_window_pos_x", main_window_pos.x);
            vessel_node.AddValue("main_window_pos_y", main_window_pos.y);
            vessel_node.AddValue("ui_icon_pos_x", ui_icon_pos.x);
            vessel_node.AddValue("ui_icon_pos_y", ui_icon_pos.y);
            vessel_node.AddValue("main_gui_minimized", main_gui_minimized);
            vessel_node.AddValue("skin_index", skin_index);
            vessel_node.AddValue("active_menu", active_menu);
            vessel_node.AddValue("remotetech_toggle", remotetech_toggle);

            vessel_node.AddValue("chatter_freq", chatter_freq);
            vessel_node.AddValue("chatter_vol_slider", chatter_vol_slider);
            vessel_node.AddValue("chatter_sel_filter", chatter_sel_filter);
            vessel_node.AddValue("show_chatter_filter_settings", show_chatter_filter_settings);
            vessel_node.AddValue("show_sample_selector", show_sample_selector);
            vessel_node.AddValue("chatter_reverb_preset_index", chatter_reverb_preset_index);
            vessel_node.AddValue("chatter_filter_settings_window_pos_x", chatter_filter_settings_window_pos.x);
            vessel_node.AddValue("chatter_filter_settings_window_pos_y", chatter_filter_settings_window_pos.y);

            vessel_node.AddValue("quindar_toggle", quindar_toggle);
            vessel_node.AddValue("quindar_vol_slider", quindar_vol_slider);
            vessel_node.AddValue("sstv_freq", sstv_freq);
            vessel_node.AddValue("sstv_vol_slider", sstv_vol_slider);

            vessel_node.AddValue("sel_beep_src", sel_beep_src);
            vessel_node.AddValue("sel_beep_page", sel_beep_page);


            //Chatter sets
            foreach (RBRAudioList chatter_set in chatter_array)
            {
                ConfigNode _set = new ConfigNode();
                _set.name = "AUDIOSET";
                _set.AddValue("directory", chatter_set.directory);
                _set.AddValue("is_active", chatter_set.is_active);
                vessel_node.AddNode(_set);
            }

            //filters
            ConfigNode _filter;

            _filter = new ConfigNode();
            _filter.name = "CHORUS";
            _filter.AddValue("enabled", chatter_chorus_filter.enabled);
            _filter.AddValue("dry_mix", chatter_chorus_filter.dryMix);
            _filter.AddValue("wet_mix_1", chatter_chorus_filter.wetMix1);
            _filter.AddValue("wet_mix_2", chatter_chorus_filter.wetMix2);
            _filter.AddValue("wet_mix_3", chatter_chorus_filter.wetMix3);
            vessel_node.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "DISTORTION";
            _filter.AddValue("enabled", chatter_distortion_filter.enabled);
            _filter.AddValue("distortion_level", chatter_distortion_filter.distortionLevel);
            vessel_node.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "ECHO";
            _filter.AddValue("enabled", chatter_echo_filter.enabled);
            _filter.AddValue("delay", chatter_echo_filter.delay);
            _filter.AddValue("decay_ratio", chatter_echo_filter.decayRatio);
            _filter.AddValue("dry_mix", chatter_echo_filter.dryMix);
            _filter.AddValue("wet_mix", chatter_echo_filter.wetMix);
            vessel_node.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "HIGHPASS";
            _filter.AddValue("enabled", chatter_highpass_filter.enabled);
            _filter.AddValue("cutoff_freq", chatter_highpass_filter.cutoffFrequency);
            _filter.AddValue("resonance_q", chatter_highpass_filter.highpassResonaceQ);
            vessel_node.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "LOWPASS";
            _filter.AddValue("enabled", chatter_lowpass_filter.enabled);
            _filter.AddValue("cutoff_freq", chatter_lowpass_filter.cutoffFrequency);
            _filter.AddValue("resonance_q", chatter_lowpass_filter.lowpassResonaceQ);
            vessel_node.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "REVERB";
            _filter.AddValue("enabled", chatter_reverb_filter.enabled);
            _filter.AddValue("reverb_preset", chatter_reverb_filter.reverbPreset);
            _filter.AddValue("dry_level", chatter_reverb_filter.dryLevel);
            _filter.AddValue("room", chatter_reverb_filter.room);
            _filter.AddValue("room_hf", chatter_reverb_filter.roomHF);
            _filter.AddValue("room_lf", chatter_reverb_filter.roomLF);
            _filter.AddValue("room_rolloff", chatter_reverb_filter.roomRolloff);
            _filter.AddValue("decay_time", chatter_reverb_filter.decayTime);
            _filter.AddValue("decay_hf_ratio", chatter_reverb_filter.decayHFRatio);
            _filter.AddValue("reflections_level", chatter_reverb_filter.reflectionsLevel);
            _filter.AddValue("reflections_delay", chatter_reverb_filter.reflectionsDelay);
            _filter.AddValue("reverb_level", chatter_reverb_filter.reverbLevel);
            _filter.AddValue("reverb_delay", chatter_reverb_filter.reverbDelay);
            _filter.AddValue("diffusion", chatter_reverb_filter.diffusion);
            _filter.AddValue("density", chatter_reverb_filter.density);
            _filter.AddValue("hf_reference", chatter_reverb_filter.hfReference);
            _filter.AddValue("lf_reference", chatter_reverb_filter.lFReference);
            vessel_node.AddNode(_filter);




            foreach (RBRBeepSource source in beepsource_list)
            {
                ConfigNode beep_settings = new ConfigNode();
                beep_settings.name = "BEEPSOURCE";

                beep_settings.AddValue("precise", source.precise);
                beep_settings.AddValue("precise_freq", source.precise_freq);
                beep_settings.AddValue("loose_freq", source.loose_freq);
                beep_settings.AddValue("volume", source.audiosource.volume);
                beep_settings.AddValue("pitch", source.audiosource.pitch);
                beep_settings.AddValue("current_clip", source.current_clip);
                beep_settings.AddValue("sel_filter", source.sel_filter);
                beep_settings.AddValue("show_settings_window", source.show_settings_window);
                beep_settings.AddValue("reverb_preset_index", source.reverb_preset_index);
                beep_settings.AddValue("settings_window_pos_x", source.settings_window_pos.x);
                beep_settings.AddValue("settings_window_pos_y", source.settings_window_pos.y);

                //filters
                //ConfigNode _filter;

                _filter = new ConfigNode();
                _filter.name = "CHORUS";
                _filter.AddValue("enabled", source.chorus_filter.enabled);
                _filter.AddValue("dry_mix", source.chorus_filter.dryMix);
                _filter.AddValue("wet_mix_1", source.chorus_filter.wetMix1);
                _filter.AddValue("wet_mix_2", source.chorus_filter.wetMix2);
                _filter.AddValue("wet_mix_3", source.chorus_filter.wetMix3);
                beep_settings.AddNode(_filter);

                _filter = new ConfigNode();
                _filter.name = "DISTORTION";
                _filter.AddValue("enabled", source.distortion_filter.enabled);
                _filter.AddValue("distortion_level", source.distortion_filter.distortionLevel);
                beep_settings.AddNode(_filter);

                _filter = new ConfigNode();
                _filter.name = "ECHO";
                _filter.AddValue("enabled", source.echo_filter.enabled);
                _filter.AddValue("delay", source.echo_filter.delay);
                _filter.AddValue("decay_ratio", source.echo_filter.decayRatio);
                _filter.AddValue("dry_mix", source.echo_filter.dryMix);
                _filter.AddValue("wet_mix", source.echo_filter.wetMix);
                beep_settings.AddNode(_filter);

                _filter = new ConfigNode();
                _filter.name = "HIGHPASS";
                _filter.AddValue("enabled", source.highpass_filter.enabled);
                _filter.AddValue("cutoff_freq", source.highpass_filter.cutoffFrequency);
                _filter.AddValue("resonance_q", source.highpass_filter.highpassResonaceQ);
                beep_settings.AddNode(_filter);

                _filter = new ConfigNode();
                _filter.name = "LOWPASS";
                _filter.AddValue("enabled", source.lowpass_filter.enabled);
                _filter.AddValue("cutoff_freq", source.lowpass_filter.cutoffFrequency);
                _filter.AddValue("resonance_q", source.lowpass_filter.lowpassResonaceQ);
                beep_settings.AddNode(_filter);

                _filter = new ConfigNode();
                _filter.name = "REVERB";
                _filter.AddValue("enabled", source.reverb_filter.enabled);
                _filter.AddValue("reverb_preset", source.reverb_filter.reverbPreset);
                _filter.AddValue("dry_level", source.reverb_filter.dryLevel);
                _filter.AddValue("room", source.reverb_filter.room);
                _filter.AddValue("room_hf", source.reverb_filter.roomHF);
                _filter.AddValue("room_lf", source.reverb_filter.roomLF);
                _filter.AddValue("room_rolloff", source.reverb_filter.roomRolloff);
                _filter.AddValue("decay_time", source.reverb_filter.decayTime);
                _filter.AddValue("decay_hf_ratio", source.reverb_filter.decayHFRatio);
                _filter.AddValue("reflections_level", source.reverb_filter.reflectionsLevel);
                _filter.AddValue("reflections_delay", source.reverb_filter.reflectionsDelay);
                _filter.AddValue("reverb_level", source.reverb_filter.reverbLevel);
                _filter.AddValue("reverb_delay", source.reverb_filter.reverbDelay);
                _filter.AddValue("diffusion", source.reverb_filter.diffusion);
                _filter.AddValue("density", source.reverb_filter.density);
                _filter.AddValue("hf_reference", source.reverb_filter.hfReference);
                _filter.AddValue("lf_reference", source.reverb_filter.lFReference);
                beep_settings.AddNode(_filter);

                vessel_node.AddNode(beep_settings);
            }
            */
            vessel_settings_node.AddNode(vessel_node);
            vessel_settings_node.Save(settings_path + "vessels.cfg");


            //if (debugging) Debug.Log("[CHATR] new_vessel_node() :: vessel_node added to vessel_settings_node");
        }

        private void load_vessel_settings_node()
        {
            //if (debugging) Debug.Log("[CHATR] START load_vessel_settings_node()");
            vessel_settings_node = ConfigNode.Load(settings_path + "vessels.cfg");

            if (vessel_settings_node != null)
            {
                if (debugging) Debug.Log("[CHATR] load_vessel_settings_node() :: vessel_settings.cfg loaded OK");
                //now search for a matching vessel_id
                //search_vessel_settings_node();
            }
            else
            {
                if (debugging) Debug.LogWarning("[CHATR] load_vessel_settings_node() :: vessel_settings.cfg is null, creating a new one");
                vessel_settings_node = new ConfigNode();
                vessel_settings_node.name = "FLIGHTS";
                new_vessel_node(vessel);  //add current vessel to vessel_settings_node
                //save_vessel_settings_node();
                //if (debugging) Debug.Log("[CHATR] load_vessel_settings_node() :: current vessel node saved to vessel_settings.cfg");
            }

        }

        private void load_vessel_node(ConfigNode node)
        {
            if (debugging) Debug.Log("[CHATR] load_vessel_node() :: loading vessel settings for this vessel from node");

            //destroy_all_beep_players();
            //destroy_all_background_players();

            load_shared_settings(node);

            if (chatter_array.Count == 0)
            {
                if (debugging) Debug.Log("[CHATR] No audiosets found in config, adding defaults");
                add_default_audiosets();
            }

            if (beepsource_list.Count == 0)
            {
                if (debugging) Debug.LogWarning("[CHATR] beepsource_list.Count == 0, adding default 3");
                add_default_beepsources();
            }

            if (backgroundsource_list.Count == 0)
            {
                if (debugging) Debug.LogWarning("[CHATR] backgroundsource_list.Count == 0, adding default 2");
                add_default_backgroundsources();
            }

            if (debugging) Debug.Log("[CHATR] load_vessel_node() :: vessel settings loaded OK : total beep sources = " + beepsource_list.Count);
        }

        private void search_vessel_settings_node()
        {
            if (debugging) Debug.Log("[CHATR] START search_vessel_settings_node()");

            bool no_match = true;

            if (debugging) Debug.Log("[CHATR] active vessel id = " + vessel.id.ToString());

            foreach (ConfigNode n in vessel_settings_node.nodes)
            {
                string val = n.GetValue("vessel_id");
                if (debugging) Debug.Log("[CHATR] n.GetValue(\"vessel_id\") = " + n.GetValue("vessel_id"));
                if (val == vessel.id.ToString())
                {
                    if (debugging) Debug.Log("[CHATR] search_vessel_settings_node() :: vessel_id match");
                    load_vessel_node(n);    //load vals
                    no_match = false;
                    break;
                }
                else if (debugging) Debug.Log("[CHATR] no match, continuing search...");
            }
            if (no_match)
            {
                if (debugging) Debug.Log("[CHATR] finished search, no vessel_id match :: creating new node for this vessel");
                //temp_vessels_node = new ConfigNode();
                new_vessel_node(vessel);
                //save_vessel_settings_node();  //done in new_vessel_node
                if (debugging) Debug.Log("[CHATR] new vessel node created and saved");
                load_chatter_audio();   //load audio in case there is none
                //return;
            }

            //save_vessel_settings_node();
        }

        private void write_vessel_settings()
        {
            //update vessel_settings.cfg here also
            //get all nodes from vessel_settings_node that are not the active vessel and put them into a list all_but_curr
            //set vessel_settings_node = all_but_curr
            //create a new node for the active vessel with its current settings
            //add new node to vessel_settings_node
            //save vessel_settings_node to .cfg
            if (debugging) Debug.Log("[CHATR] writing vessel_settings node to disk");



            ConfigNode all_but_curr_vessel = new ConfigNode();

            if (debugging) Debug.Log("[CHATR] active vessel.id = " + vessel.id.ToString());
            foreach (ConfigNode cn in vessel_settings_node.nodes)
            {
                //
                if (cn.HasValue("vessel_id"))
                {
                    string val = cn.GetValue("vessel_id");
                    if (debugging) Debug.Log("[CHATR] node vessel_id = " + val);

                    if (val != vessel.id.ToString())
                    {
                        //found an id that is not the current vessel
                        //add it to the list

                        all_but_curr_vessel.AddNode(cn);
                        if (debugging) Debug.Log("[CHATR] write_vessel_settings() :: node vessel_id != vessel.id :: node vessel added to all_but_curr_vessel");
                    }
                    //else
                    //{
                    //    all_but_prev_vessel.AddNode(cn);
                    //}
                }
            }
            //foreach (ConfigNode cn in vessel_settings_node.nodes)
            //{
            //vessel_settings_node.RemoveNodes("");
            //    if (debugging) Debug.Log("[CHATR] old nodes removed");
            //}

            vessel_settings_node = all_but_curr_vessel;
            //if (debugging) Debug.Log("[CHATR] write_vessel_settings() :: vessel_settings node = all_but_curr_vessel");

            new_vessel_node(vessel);
            //if (debugging) Debug.Log("[CHATR] write_vessel_settings() :: new node created using vessel and added to vessel_settings node");

            //save_vessel_settings_node();
            vessel_settings_node.Save(settings_path + "vessels.cfg");
            if (debugging) Debug.Log("[CHATR] write_vessel_settings() END :: vessel_settings node saved to vessel_settings.cfg");
            //end func
            if (debugging) Debug.Log("[CHATR] vessel_settings node saved to disk :: vessel node count = " + vessel_settings_node.nodes.Count);
        }

        //Set some default stuff
        private void add_default_audiosets()
        {
            chatter_array.Add(new ChatterAudioList());
            chatter_array[0].directory = "apollo11";
            chatter_array[0].is_active = true;

            chatter_array.Add(new ChatterAudioList());
            chatter_array[1].directory = "sts1";
            chatter_array[1].is_active = true;

            chatter_array.Add(new ChatterAudioList());
            chatter_array[2].directory = "russian";
            chatter_array[2].is_active = true;

            if (debugging) Debug.Log("[CHATR] audioset defaults added :: new count = " + chatter_array.Count);
        }

        private void add_default_beepsources()
        {
            for (int i = 0; i < 3; i++)
            {
                add_new_beepsource();
            }
        }

        private void add_default_backgroundsources()
        {
            for (int i = 0; i < 2; i++)
            {
                add_new_backgroundsource();
            }
        }

        private void mute_check()
        {
            //mute check
            if (mute_all)
            {
                //mute is on
                if (all_muted == false)
                {
                    //but things aren't muted
                    //mute them
                    if (chatter_exists)
                    {
                        initial_chatter.mute = true;
                        response_chatter.mute = true;
                        quindar1.mute = true;
                        quindar2.mute = true;
                    }

                    foreach (BackgroundSource src in backgroundsource_list)
                    {
                        src.audiosource.mute = true;
                    }

                    if (aae_breathing_exist) aae_breathing.mute = true;
                    if (aae_soundscapes_exist) aae_soundscape.mute = true;
                    if (aae_breathing_exist) aae_wind.mute = true;
                    if (aae_airlock_exist) aae_airlock.mute = true;

                    foreach (BeepSource source in beepsource_list)
                    {
                        source.audiosource.mute = true;
                    }
                    
                    if (sstv_exists) sstv.mute = true;

                    all_muted = true;   //and change flag
                }
            }
            else
            {
                //mute is off
                if (all_muted)
                {
                    //but things are muted
                    //unmute them
                    if (chatter_exists)
                    {
                        initial_chatter.mute = false;
                        response_chatter.mute = false;
                        quindar1.mute = false;
                        quindar2.mute = false;
                    }

                    foreach (BackgroundSource src in backgroundsource_list)
                    {
                        src.audiosource.mute = false;
                    }

                    if (aae_breathing_exist) aae_breathing.mute = false;
                    if (aae_soundscapes_exist) aae_soundscape.mute = false;
                    if (aae_wind_exist) aae_wind.mute = false;
                    if (aae_airlock_exist) aae_airlock.mute = false;

                    foreach (BeepSource source in beepsource_list)
                    {
                        source.audiosource.mute = false;
                    }
                    
                    if (sstv_exists) sstv.mute = false;

                    all_muted = false;   //and change flag
                }
            }
        }

        private void radio_check()
        {
            foreach (char c in Input.inputString)
            {
                //backspace char
                if (c == "\b"[0])
                {
                    if (yep_yep.Length != 0)
                    {
                        yep_yep = yep_yep.Substring(0, yep_yep.Length - 1);
                    }
                }
                else
                {
                    //if (c == "\n"[0] || c == "\r"[0])
                    //{
                    //print("User entered his name: " + yep_yep);
                    //}
                    //else
                    // {
                    yep_yep += c;
                    //}
                }
            }

            if (yep_yep.Length > 5) yep_yep = yep_yep.Substring(1, 5);  //only keep 5 chars, get rid of the first

            if (http_update_check && yep_yep == "radio" && yep_yepsource.isPlaying == false)
            {
                if (debugging) Debug.Log("[CHATR] play radio");


                //need a bool that radio_yepyep is loaded ok
                if (yep_yep_loaded == false)
                {
                    load_radio();
                }

                //Play "radio"
                yep_yepsource.Play();
                yep_yep = "";
            }
        }

        //RemoteTech
        private void tooltips(Rect pos)
        {
            if (show_tooltips && GUI.tooltip != "")
            {
                float w = 5.5f * GUI.tooltip.Length;
                float x = (Event.current.mousePosition.x < pos.width / 2) ? Event.current.mousePosition.x + 10 : Event.current.mousePosition.x - 10 - w;
                GUI.Box(new Rect(x, Event.current.mousePosition.y, w, 25f), GUI.tooltip, gs_tooltip);
            }
        }

        //Reset filter
        private void reset_chorus_filter(AudioChorusFilter acf)
        {
            //reset chorus filter to default
            foreach (ConfigNode filter in filter_defaults.nodes)
            {
                if (filter.name == "CHORUS")
                {
                    if (filter.HasValue("dry_mix")) acf.dryMix = Single.Parse(filter.GetValue("dry_mix"));
                    if (filter.HasValue("wet_mix_1")) acf.wetMix1 = Single.Parse(filter.GetValue("wet_mix_1"));
                    if (filter.HasValue("wet_mix_2")) acf.wetMix2 = Single.Parse(filter.GetValue("wet_mix_2"));
                    if (filter.HasValue("wet_mix_3")) acf.wetMix3 = Single.Parse(filter.GetValue("wet_mix_3"));
                    if (filter.HasValue("delay")) acf.delay = Single.Parse(filter.GetValue("delay"));
                    if (filter.HasValue("rate")) acf.rate = Single.Parse(filter.GetValue("rate"));
                    if (filter.HasValue("depth")) acf.depth = Single.Parse(filter.GetValue("depth"));
                }
            }
        }

        private void reset_distortion_filter(AudioDistortionFilter adf)
        {
            //reset distortion filter to default
            foreach (ConfigNode filter in filter_defaults.nodes)
            {
                if (filter.name == "DISTORTION")
                {
                    if (filter.HasValue("distortion_level")) adf.distortionLevel = Single.Parse(filter.GetValue("distortion_level"));
                }
            }
        }

        private void reset_echo_filter(AudioEchoFilter aef)
        {
            //reset echo filter to default
            foreach (ConfigNode filter in filter_defaults.nodes)
            {
                if (filter.name == "ECHO")
                {
                    if (filter.HasValue("delay")) aef.delay = Single.Parse(filter.GetValue("delay"));
                    if (filter.HasValue("decay_ratio")) aef.decayRatio = Single.Parse(filter.GetValue("decay_ratio"));
                    if (filter.HasValue("dry_mix")) aef.dryMix = Single.Parse(filter.GetValue("dry_mix"));
                    if (filter.HasValue("wet_mix")) aef.wetMix = Single.Parse(filter.GetValue("wet_mix"));
                }
            }
        }

        private void reset_highpass_filter(AudioHighPassFilter ahpf)
        {
            //reset highpass filter to default
            foreach (ConfigNode filter in filter_defaults.nodes)
            {
                if (filter.name == "HIGHPASS")
                {
                    if (filter.HasValue("cutoff_freq")) ahpf.cutoffFrequency = Single.Parse(filter.GetValue("cutoff_freq"));
                    if (filter.HasValue("resonance_q")) ahpf.highpassResonaceQ = Single.Parse(filter.GetValue("resonance_q"));
                }
            }
        }

        private void reset_lowpass_filter(AudioLowPassFilter alpf)
        {
            //reset lowpass filter to default
            foreach (ConfigNode filter in filter_defaults.nodes)
            {
                if (filter.name == "LOWPASS")
                {
                    if (filter.HasValue("cutoff_freq")) alpf.cutoffFrequency = Single.Parse(filter.GetValue("cutoff_freq"));
                    if (filter.HasValue("resonance_q")) alpf.lowpassResonaceQ = Single.Parse(filter.GetValue("resonance_q"));
                }
            }
        }

        private void reset_reverb_filter(AudioReverbFilter arf)
        {
            //reset reverb filter to default
            foreach (ConfigNode filter in filter_defaults.nodes)
            {
                if (filter.name == "REVERB")
                {
                    if (filter.HasValue("reverb_preset")) arf.reverbPreset = (AudioReverbPreset)Enum.Parse(typeof(AudioReverbPreset), filter.GetValue("reverb_preset"));
                    if (filter.HasValue("dry_level")) arf.dryLevel = Single.Parse(filter.GetValue("dry_level"));
                    if (filter.HasValue("room")) arf.room = Single.Parse(filter.GetValue("room"));
                    if (filter.HasValue("room_hf")) arf.roomHF = Single.Parse(filter.GetValue("room_hf"));
                    if (filter.HasValue("room_lf")) arf.roomLF = Single.Parse(filter.GetValue("room_lf"));
                    if (filter.HasValue("room_rolloff")) arf.roomRolloff = Single.Parse(filter.GetValue("room_rolloff"));
                    if (filter.HasValue("decay_time")) arf.decayTime = Single.Parse(filter.GetValue("decay_time"));
                    if (filter.HasValue("decay_hf_ratio")) arf.decayHFRatio = Single.Parse(filter.GetValue("decay_hf_ratio"));
                    if (filter.HasValue("reflections_level")) arf.reflectionsLevel = Single.Parse(filter.GetValue("reflections_level"));
                    if (filter.HasValue("reflections_delay")) arf.reflectionsDelay = Single.Parse(filter.GetValue("reflections_delay"));
                    if (filter.HasValue("reverb_level")) arf.reverbLevel = Single.Parse(filter.GetValue("reverb_level"));
                    if (filter.HasValue("reverb_delay")) arf.reverbDelay = Single.Parse(filter.GetValue("reverb_delay"));
                    if (filter.HasValue("diffusion")) arf.diffusion = Single.Parse(filter.GetValue("diffusion"));
                    if (filter.HasValue("density")) arf.density = Single.Parse(filter.GetValue("density"));
                    if (filter.HasValue("hf_reference")) arf.hfReference = Single.Parse(filter.GetValue("hf_reference"));
                    if (filter.HasValue("lf_reference")) arf.lFReference = Single.Parse(filter.GetValue("lf_reference"));
                }
            }
        }

        //Copy/Paste chatter filters
        private void copy_all_chatter_filters()
        {
            //copy all chatter filter settings to a temp clipboard ConfigNode

            filters_clipboard = new ConfigNode();

            ConfigNode _filter;

            _filter = new ConfigNode();
            _filter.name = "CHORUS";
            _filter.AddValue("enabled", chatter_chorus_filter.enabled);
            _filter.AddValue("dry_mix", chatter_chorus_filter.dryMix);
            _filter.AddValue("wet_mix_1", chatter_chorus_filter.wetMix1);
            _filter.AddValue("wet_mix_2", chatter_chorus_filter.wetMix2);
            _filter.AddValue("wet_mix_3", chatter_chorus_filter.wetMix3);
            _filter.AddValue("delay", chatter_chorus_filter.delay);
            _filter.AddValue("rate", chatter_chorus_filter.rate);
            _filter.AddValue("depth", chatter_chorus_filter.depth);
            filters_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "DISTORTION";
            _filter.AddValue("enabled", chatter_distortion_filter.enabled);
            _filter.AddValue("distortion_level", chatter_distortion_filter.distortionLevel);
            filters_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "ECHO";
            _filter.AddValue("enabled", chatter_echo_filter.enabled);
            _filter.AddValue("delay", chatter_echo_filter.delay);
            _filter.AddValue("decay_ratio", chatter_echo_filter.decayRatio);
            _filter.AddValue("dry_mix", chatter_echo_filter.dryMix);
            _filter.AddValue("wet_mix", chatter_echo_filter.wetMix);
            filters_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "HIGHPASS";
            _filter.AddValue("enabled", chatter_highpass_filter.enabled);
            _filter.AddValue("cutoff_freq", chatter_highpass_filter.cutoffFrequency);
            _filter.AddValue("resonance_q", chatter_highpass_filter.highpassResonaceQ);
            filters_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "LOWPASS";
            _filter.AddValue("enabled", chatter_lowpass_filter.enabled);
            _filter.AddValue("cutoff_freq", chatter_lowpass_filter.cutoffFrequency);
            _filter.AddValue("resonance_q", chatter_lowpass_filter.lowpassResonaceQ);
            filters_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "REVERB";
            _filter.AddValue("enabled", chatter_reverb_filter.enabled);
            _filter.AddValue("reverb_preset", chatter_reverb_filter.reverbPreset);
            _filter.AddValue("dry_level", chatter_reverb_filter.dryLevel);
            _filter.AddValue("room", chatter_reverb_filter.room);
            _filter.AddValue("room_hf", chatter_reverb_filter.roomHF);
            _filter.AddValue("room_lf", chatter_reverb_filter.roomLF);
            _filter.AddValue("room_rolloff", chatter_reverb_filter.roomRolloff);
            _filter.AddValue("decay_time", chatter_reverb_filter.decayTime);
            _filter.AddValue("decay_hf_ratio", chatter_reverb_filter.decayHFRatio);
            _filter.AddValue("reflections_level", chatter_reverb_filter.reflectionsLevel);
            _filter.AddValue("reflections_delay", chatter_reverb_filter.reflectionsDelay);
            _filter.AddValue("reverb_level", chatter_reverb_filter.reverbLevel);
            _filter.AddValue("reverb_delay", chatter_reverb_filter.reverbDelay);
            _filter.AddValue("diffusion", chatter_reverb_filter.diffusion);
            _filter.AddValue("density", chatter_reverb_filter.density);
            _filter.AddValue("hf_reference", chatter_reverb_filter.hfReference);
            _filter.AddValue("lf_reference", chatter_reverb_filter.lFReference);
            filters_clipboard.AddNode(_filter);

            if (debugging) Debug.Log("[CHATR] all chatter filter values copied to filters_clipboard");
        }

        private void paste_all_chatter_filters()
        {
            ConfigNode filter = new ConfigNode();

            filter = filters_clipboard.GetNode("CHORUS");
            if (filter.HasValue("enabled")) chatter_chorus_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
            if (filter.HasValue("dry_mix")) chatter_chorus_filter.dryMix = Single.Parse(filter.GetValue("dry_mix"));
            if (filter.HasValue("wet_mix_1")) chatter_chorus_filter.wetMix1 = Single.Parse(filter.GetValue("wet_mix_1"));
            if (filter.HasValue("wet_mix_2")) chatter_chorus_filter.wetMix2 = Single.Parse(filter.GetValue("wet_mix_2"));
            if (filter.HasValue("wet_mix_3")) chatter_chorus_filter.wetMix3 = Single.Parse(filter.GetValue("wet_mix_3"));
            if (filter.HasValue("delay")) chatter_chorus_filter.delay = Single.Parse(filter.GetValue("delay"));
            if (filter.HasValue("rate")) chatter_chorus_filter.rate = Single.Parse(filter.GetValue("rate"));
            if (filter.HasValue("depth")) chatter_chorus_filter.depth = Single.Parse(filter.GetValue("depth"));

            filter = filters_clipboard.GetNode("DISTORTION");
            if (filter.HasValue("enabled")) chatter_distortion_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
            if (filter.HasValue("distortion_level")) chatter_distortion_filter.distortionLevel = Single.Parse(filter.GetValue("distortion_level"));

            filter = filters_clipboard.GetNode("ECHO");
            if (filter.HasValue("enabled")) chatter_echo_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
            if (filter.HasValue("delay")) chatter_echo_filter.delay = Single.Parse(filter.GetValue("delay"));
            if (filter.HasValue("decay_ratio")) chatter_echo_filter.decayRatio = Single.Parse(filter.GetValue("decay_ratio"));
            if (filter.HasValue("dry_mix")) chatter_echo_filter.dryMix = Single.Parse(filter.GetValue("dry_mix"));
            if (filter.HasValue("wet_mix")) chatter_echo_filter.wetMix = Single.Parse(filter.GetValue("wet_mix"));

            filter = filters_clipboard.GetNode("HIGHPASS");
            if (filter.HasValue("enabled")) chatter_highpass_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
            if (filter.HasValue("cutoff_freq")) chatter_highpass_filter.cutoffFrequency = Single.Parse(filter.GetValue("cutoff_freq"));
            if (filter.HasValue("resonance_q")) chatter_highpass_filter.highpassResonaceQ = Single.Parse(filter.GetValue("resonance_q"));

            filter = filters_clipboard.GetNode("LOWPASS");
            if (filter.HasValue("enabled")) chatter_lowpass_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
            if (filter.HasValue("cutoff_freq")) chatter_lowpass_filter.cutoffFrequency = Single.Parse(filter.GetValue("cutoff_freq"));
            if (filter.HasValue("resonance_q")) chatter_lowpass_filter.lowpassResonaceQ = Single.Parse(filter.GetValue("resonance_q"));

            filter = filters_clipboard.GetNode("REVERB");
            if (filter.HasValue("enabled")) chatter_reverb_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
            if (filter.HasValue("reverb_preset")) chatter_reverb_filter.reverbPreset = (AudioReverbPreset)Enum.Parse(typeof(AudioReverbPreset), filter.GetValue("reverb_preset"));
            if (filter.HasValue("dry_level")) chatter_reverb_filter.dryLevel = Single.Parse(filter.GetValue("dry_level"));
            if (filter.HasValue("room")) chatter_reverb_filter.room = Single.Parse(filter.GetValue("room"));
            if (filter.HasValue("room_hf")) chatter_reverb_filter.roomHF = Single.Parse(filter.GetValue("room_hf"));
            if (filter.HasValue("room_lf")) chatter_reverb_filter.roomLF = Single.Parse(filter.GetValue("room_lf"));
            if (filter.HasValue("room_rolloff")) chatter_reverb_filter.roomRolloff = Single.Parse(filter.GetValue("room_rolloff"));
            if (filter.HasValue("decay_time")) chatter_reverb_filter.decayTime = Single.Parse(filter.GetValue("decay_time"));
            if (filter.HasValue("decay_hf_ratio")) chatter_reverb_filter.decayHFRatio = Single.Parse(filter.GetValue("decay_hf_ratio"));
            if (filter.HasValue("reflections_level")) chatter_reverb_filter.reflectionsLevel = Single.Parse(filter.GetValue("reflections_level"));
            if (filter.HasValue("reflections_delay")) chatter_reverb_filter.reflectionsDelay = Single.Parse(filter.GetValue("reflections_delay"));
            if (filter.HasValue("reverb_level")) chatter_reverb_filter.reverbLevel = Single.Parse(filter.GetValue("reverb_level"));
            if (filter.HasValue("reverb_delay")) chatter_reverb_filter.reverbDelay = Single.Parse(filter.GetValue("reverb_delay"));
            if (filter.HasValue("diffusion")) chatter_reverb_filter.diffusion = Single.Parse(filter.GetValue("diffusion"));
            if (filter.HasValue("density")) chatter_reverb_filter.density = Single.Parse(filter.GetValue("density"));
            if (filter.HasValue("hf_reference")) chatter_reverb_filter.hfReference = Single.Parse(filter.GetValue("hf_reference"));
            if (filter.HasValue("lf_reference")) chatter_reverb_filter.lFReference = Single.Parse(filter.GetValue("lf_reference"));

            if (debugging) Debug.Log("[CHATR] all chatter filter values pasted from filters_clipboard");
        }

        //Copy/Paste beep filters
        private void copy_all_beep_filters(BeepSource source)
        {
            filters_clipboard = new ConfigNode();
            ConfigNode _filter;

            _filter = new ConfigNode();
            _filter.name = "CHORUS";
            _filter.AddValue("enabled", source.chorus_filter.enabled);
            _filter.AddValue("dry_mix", source.chorus_filter.dryMix);
            _filter.AddValue("wet_mix_1", source.chorus_filter.wetMix1);
            _filter.AddValue("wet_mix_2", source.chorus_filter.wetMix2);
            _filter.AddValue("wet_mix_3", source.chorus_filter.wetMix3);
            _filter.AddValue("delay", source.chorus_filter.delay);
            _filter.AddValue("rate", source.chorus_filter.rate);
            _filter.AddValue("depth", source.chorus_filter.depth);
            filters_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "DISTORTION";
            _filter.AddValue("enabled", source.distortion_filter.enabled);
            _filter.AddValue("distortion_level", source.distortion_filter.distortionLevel);
            filters_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "ECHO";
            _filter.AddValue("enabled", source.echo_filter.enabled);
            _filter.AddValue("delay", source.echo_filter.delay);
            _filter.AddValue("decay_ratio", source.echo_filter.decayRatio);
            _filter.AddValue("dry_mix", source.echo_filter.dryMix);
            _filter.AddValue("wet_mix", source.echo_filter.wetMix);
            filters_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "HIGHPASS";
            _filter.AddValue("enabled", source.highpass_filter.enabled);
            _filter.AddValue("cutoff_freq", source.highpass_filter.cutoffFrequency);
            _filter.AddValue("resonance_q", source.highpass_filter.highpassResonaceQ);
            filters_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "LOWPASS";
            _filter.AddValue("enabled", source.lowpass_filter.enabled);
            _filter.AddValue("cutoff_freq", source.lowpass_filter.cutoffFrequency);
            _filter.AddValue("resonance_q", source.lowpass_filter.lowpassResonaceQ);
            filters_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "REVERB";
            _filter.AddValue("enabled", source.reverb_filter.enabled);
            _filter.AddValue("reverb_preset", source.reverb_filter.reverbPreset);
            _filter.AddValue("dry_level", source.reverb_filter.dryLevel);
            _filter.AddValue("room", source.reverb_filter.room);
            _filter.AddValue("room_hf", source.reverb_filter.roomHF);
            _filter.AddValue("room_lf", source.reverb_filter.roomLF);
            _filter.AddValue("room_rolloff", source.reverb_filter.roomRolloff);
            _filter.AddValue("decay_time", source.reverb_filter.decayTime);
            _filter.AddValue("decay_hf_ratio", source.reverb_filter.decayHFRatio);
            _filter.AddValue("reflections_level", source.reverb_filter.reflectionsLevel);
            _filter.AddValue("reflections_delay", source.reverb_filter.reflectionsDelay);
            _filter.AddValue("reverb_level", source.reverb_filter.reverbLevel);
            _filter.AddValue("reverb_delay", source.reverb_filter.reverbDelay);
            _filter.AddValue("diffusion", source.reverb_filter.diffusion);
            _filter.AddValue("density", source.reverb_filter.density);
            _filter.AddValue("hf_reference", source.reverb_filter.hfReference);
            _filter.AddValue("lf_reference", source.reverb_filter.lFReference);
            filters_clipboard.AddNode(_filter);

            if (debugging) Debug.Log("[CHATR] all beep filter values copied to filters_clipboard");
        }

        private void paste_all_beep_filters(BeepSource source)
        {
            ConfigNode filter = new ConfigNode();

            filter = filters_clipboard.GetNode("CHORUS");
            if (filter.HasValue("enabled")) source.chorus_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
            if (filter.HasValue("dry_mix")) source.chorus_filter.dryMix = Single.Parse(filter.GetValue("dry_mix"));
            if (filter.HasValue("wet_mix_1")) source.chorus_filter.wetMix1 = Single.Parse(filter.GetValue("wet_mix_1"));
            if (filter.HasValue("wet_mix_2")) source.chorus_filter.wetMix2 = Single.Parse(filter.GetValue("wet_mix_2"));
            if (filter.HasValue("wet_mix_3")) source.chorus_filter.wetMix3 = Single.Parse(filter.GetValue("wet_mix_3"));
            if (filter.HasValue("delay")) source.chorus_filter.dryMix = Single.Parse(filter.GetValue("delay"));
            if (filter.HasValue("rate")) source.chorus_filter.dryMix = Single.Parse(filter.GetValue("rate"));
            if (filter.HasValue("depth")) source.chorus_filter.dryMix = Single.Parse(filter.GetValue("depth"));

            filter = filters_clipboard.GetNode("DISTORTION");
            if (filter.HasValue("enabled")) source.distortion_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
            if (filter.HasValue("distortion_level")) source.distortion_filter.distortionLevel = Single.Parse(filter.GetValue("distortion_level"));

            filter = filters_clipboard.GetNode("ECHO");
            if (filter.HasValue("enabled")) source.echo_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
            if (filter.HasValue("delay")) source.echo_filter.delay = Single.Parse(filter.GetValue("delay"));
            if (filter.HasValue("decay_ratio")) source.echo_filter.decayRatio = Single.Parse(filter.GetValue("decay_ratio"));
            if (filter.HasValue("dry_mix")) source.echo_filter.dryMix = Single.Parse(filter.GetValue("dry_mix"));
            if (filter.HasValue("wet_mix")) source.echo_filter.wetMix = Single.Parse(filter.GetValue("wet_mix"));

            filter = filters_clipboard.GetNode("HIGHPASS");
            if (filter.HasValue("enabled")) source.highpass_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
            if (filter.HasValue("cutoff_freq")) source.highpass_filter.cutoffFrequency = Single.Parse(filter.GetValue("cutoff_freq"));
            if (filter.HasValue("resonance_q")) source.highpass_filter.highpassResonaceQ = Single.Parse(filter.GetValue("resonance_q"));

            filter = filters_clipboard.GetNode("LOWPASS");
            if (filter.HasValue("enabled")) source.lowpass_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
            if (filter.HasValue("cutoff_freq")) source.lowpass_filter.cutoffFrequency = Single.Parse(filter.GetValue("cutoff_freq"));
            if (filter.HasValue("resonance_q")) source.lowpass_filter.lowpassResonaceQ = Single.Parse(filter.GetValue("resonance_q"));

            filter = filters_clipboard.GetNode("REVERB");
            if (filter.HasValue("enabled")) source.reverb_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
            if (filter.HasValue("reverb_preset")) source.reverb_filter.reverbPreset = (AudioReverbPreset)Enum.Parse(typeof(AudioReverbPreset), filter.GetValue("reverb_preset"));
            if (filter.HasValue("dry_level")) source.reverb_filter.dryLevel = Single.Parse(filter.GetValue("dry_level"));
            if (filter.HasValue("room")) source.reverb_filter.room = Single.Parse(filter.GetValue("room"));
            if (filter.HasValue("room_hf")) source.reverb_filter.roomHF = Single.Parse(filter.GetValue("room_hf"));
            if (filter.HasValue("room_lf")) source.reverb_filter.roomLF = Single.Parse(filter.GetValue("room_lf"));
            if (filter.HasValue("room_rolloff")) source.reverb_filter.roomRolloff = Single.Parse(filter.GetValue("room_rolloff"));
            if (filter.HasValue("decay_time")) source.reverb_filter.decayTime = Single.Parse(filter.GetValue("decay_time"));
            if (filter.HasValue("decay_hf_ratio")) source.reverb_filter.decayHFRatio = Single.Parse(filter.GetValue("decay_hf_ratio"));
            if (filter.HasValue("reflections_level")) source.reverb_filter.reflectionsLevel = Single.Parse(filter.GetValue("reflections_level"));
            if (filter.HasValue("reflections_delay")) source.reverb_filter.reflectionsDelay = Single.Parse(filter.GetValue("reflections_delay"));
            if (filter.HasValue("reverb_level")) source.reverb_filter.reverbLevel = Single.Parse(filter.GetValue("reverb_level"));
            if (filter.HasValue("reverb_delay")) source.reverb_filter.reverbDelay = Single.Parse(filter.GetValue("reverb_delay"));
            if (filter.HasValue("diffusion")) source.reverb_filter.diffusion = Single.Parse(filter.GetValue("diffusion"));
            if (filter.HasValue("density")) source.reverb_filter.density = Single.Parse(filter.GetValue("density"));
            if (filter.HasValue("hf_reference")) source.reverb_filter.hfReference = Single.Parse(filter.GetValue("hf_reference"));
            if (filter.HasValue("lf_reference")) source.reverb_filter.lFReference = Single.Parse(filter.GetValue("lf_reference"));

            if (debugging) Debug.Log("[CHATR] all beep filter values pasted from filters_clipboard");
        }

        //Main
        public void Awake()
        {
            if (debugging) Debug.Log("[CHATR] Awake() starting...");

            //set a path to save/load settings
            settings_path = AssemblyLoader.loadedAssemblies.GetPathByType(typeof(chatterer)) + "/"; //returns "X:/full/path/to/GameData/Chatterer/Plugins/PluginData/chatterer"

            if (Directory.Exists(settings_path))
            {
                if (debugging) Debug.Log("[CHATR] " + settings_path + " exists");
            }
            else
            {
                if (debugging) Debug.Log("[CHATR] " + settings_path + " does not exist");
                Directory.CreateDirectory(settings_path);
                if (debugging) Debug.Log("[CHATR] " + settings_path + " created");
            }

            //Filters need to be added here BEFORE load_settings() or nullRef when trying to apply filter settings to non-existant filters
            chatter_player.name = "rbr_chatter_player";
            initial_chatter = chatter_player.AddComponent<AudioSource>();
            initial_chatter.volume = chatter_vol_slider;
            initial_chatter.panLevel = 0;   //set as 2D audio
            response_chatter = chatter_player.AddComponent<AudioSource>();
            response_chatter.volume = chatter_vol_slider;
            response_chatter.panLevel = 0;
            chatter_chorus_filter = chatter_player.AddComponent<AudioChorusFilter>();
            chatter_chorus_filter.enabled = false;
            chatter_distortion_filter = chatter_player.AddComponent<AudioDistortionFilter>();
            chatter_distortion_filter.enabled = false;
            chatter_echo_filter = chatter_player.AddComponent<AudioEchoFilter>();
            chatter_echo_filter.enabled = false;
            chatter_highpass_filter = chatter_player.AddComponent<AudioHighPassFilter>();
            chatter_highpass_filter.enabled = false;
            chatter_lowpass_filter = chatter_player.AddComponent<AudioLowPassFilter>();
            chatter_lowpass_filter.enabled = false;
            chatter_reverb_filter = chatter_player.AddComponent<AudioReverbFilter>();
            chatter_reverb_filter.enabled = false;


            //AAE
            load_AAE_background_audio();
            load_AAE_soundscape_audio();

            if (aae_soundscapes_exist)
            {
                aae_soundscape = aae_soundscape_player.AddComponent<AudioSource>();
                aae_soundscape.panLevel = 0;
                aae_soundscape.volume = 0.3f;
                set_soundscape_clip();
                new_soundscape_loose_timer_limit();
            }

            //AAE EVA breathing
            aae_breathing = aae_ambient_player.AddComponent<AudioSource>();
            aae_breathing.panLevel = 0;
            aae_breathing.volume = 1.0f;
            aae_breathing.loop = true;
            string breathing_path = "Chatterer/Sounds/AAE/effect/breathing";
            if (GameDatabase.Instance.ExistsAudioClip(breathing_path))
            {
                aae_breathing_exist = true;
                aae_breathing.clip = GameDatabase.Instance.GetAudioClip(breathing_path);
                if (debugging) Debug.Log("[CHATR] " + breathing_path + " loaded OK");
            }
            else
            {
                if (debugging) Debug.LogWarning("[CHATR] " + breathing_path + " not found");
            }

            //AAE airlock
            aae_airlock = aae_ambient_player.AddComponent<AudioSource>();
            aae_airlock.panLevel = 0;
            aae_airlock.volume = 1.0f;
            string airlock_path = "Chatterer/Sounds/AAE/effect/airlock";
            if (GameDatabase.Instance.ExistsAudioClip(airlock_path))
            {
                aae_airlock_exist = true;
                aae_airlock.clip = GameDatabase.Instance.GetAudioClip(airlock_path);
                if (debugging) Debug.Log("[CHATR] " + airlock_path + " loaded OK");
            }
            else
            {
                if (debugging) Debug.LogWarning("[CHATR] " + airlock_path + " not found");
            }

            //AAE wind
            aae_wind = aae_ambient_player.AddComponent<AudioSource>();
            aae_wind.panLevel = 0;
            aae_wind.volume = 1.0f;
            string wind_path = "Chatterer/Sounds/AAE/wind/mario1298__weak-wind";
            if (GameDatabase.Instance.ExistsAudioClip(wind_path))
            {
                aae_wind_exist = true;
                aae_wind.clip = GameDatabase.Instance.GetAudioClip(wind_path);
                if (debugging) Debug.Log("[CHATR] " + wind_path + " loaded OK");
            }
            else
            {
                if (debugging) Debug.LogWarning("[CHATR] " + wind_path + " not found");
            }

            //yepyep
            yep_yepsource = aae_ambient_player.AddComponent<AudioSource>();
            yep_yepsource.panLevel = 0;
            yep_yepsource.volume = 1.0f;

            //AAE landing
            landingsource = aae_ambient_player.AddComponent<AudioSource>();
            landingsource.panLevel = 0;
            landingsource.volume = 0.5f;
            string landing_path = "Chatterer/Sounds/AAE/loop/suspense1";
            if (GameDatabase.Instance.ExistsAudioClip(landing_path))
            {
                landingsource.clip = GameDatabase.Instance.GetAudioClip(landing_path);
                if (debugging) Debug.Log("[CHATR] " + landing_path + " loaded OK");
            }
            else
            {
                if (debugging) Debug.LogWarning("[CHATR] " + landing_path + " not found");
            }



            load_beep_audio();      //this must run before loading settings (else no beep clips to assign to sources))

            if (GameDatabase.Instance.ExistsTexture("Chatterer/Textures/line_512x4")) line_512x4 = GameDatabase.Instance.GetTexture("Chatterer/Textures/line_512x4", false);
            else Debug.LogWarning("Texture 'line_512x4' is missing!");

            // initialise launcherButton textures
            if (GameDatabase.Instance.ExistsTexture("Chatterer/Textures/chatterer_button_TX")) chatterer_button_TX = GameDatabase.Instance.GetTexture("Chatterer/Textures/chatterer_button_TX", false);
            if (GameDatabase.Instance.ExistsTexture("Chatterer/Textures/chatterer_button_TX_muted")) chatterer_button_TX_muted = GameDatabase.Instance.GetTexture("Chatterer/Textures/chatterer_button_TX_muted", false);
            if (GameDatabase.Instance.ExistsTexture("Chatterer/Textures/chatterer_button_RX")) chatterer_button_RX = GameDatabase.Instance.GetTexture("Chatterer/Textures/chatterer_button_RX", false);
            if (GameDatabase.Instance.ExistsTexture("Chatterer/Textures/chatterer_button_RX_muted")) chatterer_button_RX_muted = GameDatabase.Instance.GetTexture("Chatterer/Textures/chatterer_button_RX_muted", false);
            if (GameDatabase.Instance.ExistsTexture("Chatterer/Textures/chatterer_button_SSTV")) chatterer_button_SSTV = GameDatabase.Instance.GetTexture("Chatterer/Textures/chatterer_button_SSTV", false);
            if (GameDatabase.Instance.ExistsTexture("Chatterer/Textures/chatterer_button_SSTV_muted")) chatterer_button_SSTV_muted = GameDatabase.Instance.GetTexture("Chatterer/Textures/chatterer_button_SSTV_muted", false);
            if (GameDatabase.Instance.ExistsTexture("Chatterer/Textures/chatterer_button_idle")) chatterer_button_idle = GameDatabase.Instance.GetTexture("Chatterer/Textures/chatterer_button_idle", false);
            if (GameDatabase.Instance.ExistsTexture("Chatterer/Textures/chatterer_button_idle_muted")) chatterer_button_idle_muted = GameDatabase.Instance.GetTexture("Chatterer/Textures/chatterer_button_idle_muted", false);
            //if (GameDatabase.Instance.ExistsTexture("Chatterer/Textures/chatterer_button_disabled")) chatterer_button_disabled = GameDatabase.Instance.GetTexture("Chatterer/Textures/chatterer_button_disabled", false); // for later RT2 use
            //if (GameDatabase.Instance.ExistsTexture("Chatterer/Textures/chatterer_button_disabled_muted")) chatterer_button_disabled_muted = GameDatabase.Instance.GetTexture("Chatterer/Textures/chatterer_button_disabled_muted", false);

            load_plugin_settings();


            load_quindar_audio();
            quindar1 = chatter_player.AddComponent<AudioSource>();
            quindar1.volume = quindar_vol_slider;
            quindar1.panLevel = 0;
            quindar1.clip = quindar_clip;
            quindar2 = chatter_player.AddComponent<AudioSource>();
            quindar2.volume = quindar_vol_slider;
            quindar2.panLevel = 0;
            quindar2.clip = quindar_clip;

            initialize_new_exchange();

            load_sstv_audio();
            sstv_player.name = "rbr_sstv_player";
            sstv = sstv_player.AddComponent<AudioSource>();
            sstv.volume = sstv_vol_slider;
            sstv.panLevel = 0;

            new_sstv_loose_timer_limit();

            create_filter_defaults_node();

            build_skin_list();

            // Setup & callbacks for KSP Application Launcher
            GameEvents.onGUIApplicationLauncherReady.Add(OnGUIApplicationLauncherReady);
            GameEvents.onGameSceneLoadRequested.Add(OnSceneChangeRequest);

            if (debugging) Debug.Log("[CHATR] Awake() has finished...");
        }

        public void Update()
        {
            //Insta-... key setup
            if (insta_chatter_key_just_changed && Input.GetKeyUp(insta_chatter_key)) insta_chatter_key_just_changed = false;
            if (insta_sstv_key_just_changed && Input.GetKeyUp(insta_sstv_key)) insta_sstv_key_just_changed = false;

            ////Icon relocation
            //if (changing_icon_pos && Input.GetMouseButtonDown(0))
            //{
            //    ui_icon_pos = new Rect(Input.mousePosition.x - 15f, Screen.height - Input.mousePosition.y - 15f, 30f, 30f);
            //    changing_icon_pos = false;
            //}
            
            mute_check();

            radio_check();

            launcherButtonTexture_check();

            //// launcherButton texture change check
            ////if (inRadioContact) // for later use when RT2 support is implemented
            ////{
            //    if (initial_chatter.isPlaying) SetAppLauncherButtonTexture(chatterer_button_TX);
            //    else if (response_chatter.isPlaying) SetAppLauncherButtonTexture(chatterer_button_RX);
            //    else if (sstv.isPlaying) SetAppLauncherButtonTexture(chatterer_button_SSTV);
            //    else if (!all_muted) SetAppLauncherButtonTexture(chatterer_button_idle);
            ////}
            ////else SetAppLauncherButtonTexture(chatterer_button_disabled); 

            if (FlightGlobals.ActiveVessel != null)
            {
                vessel = FlightGlobals.ActiveVessel;

                //set num_beep_pages for use in windows
                num_beep_pages = beepsource_list.Count / 10;
                if (beepsource_list.Count % 10 != 0) num_beep_pages++;
                prev_num_pages = num_beep_pages;


                //sample selector one-time play
                if (OTP_playing && OTP_source.audiosource.isPlaying == false)
                {
                    if (debugging) Debug.Log("[CHATR] one-time play has finished");
                    OTP_playing = false;
                    OTP_source.audiosource.clip = OTP_stored_clip;
                    //if (debugging) Debug.Log("[CHATR] OTP_source.current_clip = " + OTP_source.current_clip);
                    //set_beep_clip(OTP_source);
                }

                //RUN-ONCE
                if (run_once)
                {
                    //get null refs trying to set these in Awake() so do them once here


                    prev_vessel = vessel;
                    vessel_prev_sit = vessel.situation;
                    vessel_prev_stage = vessel.currentStage;
                    vessel_part_count = vessel.parts.Count;
                    run_once = false;

                    if (use_vessel_settings)
                    {
                        if (debugging) Debug.Log("[CHATR] Update() run-once :: calling load_vessel_settings_node()");
                        load_vessel_settings_node(); //load and search for settings for this vessel
                        if (debugging) Debug.Log("[CHATR] Update() run-once :: calling search_vessel_settings_node()");
                        search_vessel_settings_node();
                    }
                }

                if (vessel != prev_vessel)
                {
                    //active vessel has changed
                    if (debugging) Debug.Log("[CHATR] ActiveVessel has changed::prev = " + prev_vessel.vesselName + ", curr = " + vessel.vesselName);

                    //stop_audio("all");


                    //play a new clip any time vessel changes and new vessel has crew or is EVA
                    //if (((power_available && vessel.GetCrewCount() > 0) || vessel.vesselType == VesselType.EVA) && chatter_freq > 0)
                    //{
                    //new active vessel has crew onboard or is EVA
                    //play an auto clip
                    //pod_begins_exchange = true;
                    //begin_exchange(0);
                    //}



                    if (use_vessel_settings)
                    {

                        ConfigNode all_but_prev_vessel = new ConfigNode();

                        if (debugging) Debug.Log("[CHATR] Update() :: checking each vessel_id in vessel_settings_node");
                        if (debugging) Debug.Log("[CHATR] prev_vessel.id = " + prev_vessel.id.ToString());
                        foreach (ConfigNode _vessel in vessel_settings_node.nodes)
                        {


                            //search for previous vessel id
                            if (_vessel.HasValue("vessel_id"))
                            {
                                string val = _vessel.GetValue("vessel_id");

                                //if (debugging) Debug.Log("[CHATR] node vessel_id = " + val);

                                if (val != prev_vessel.id.ToString())
                                {
                                    //vessel_settings_node.RemoveNode(prev_vessel.id.ToString());
                                    //if (debugging) Debug.Log("[CHATR] prev_vessel old node removed");
                                    //temp_vessels_string = prev_vessel.id.ToString();
                                    all_but_prev_vessel.AddNode(_vessel);
                                    if (debugging) Debug.Log("[CHATR] Update() :: node vessel_id != prev_vessel.id :: node vessel added to all_but_prev_vessel");
                                }
                                //else
                                //{
                                //    all_but_prev_vessel.AddNode(cn);
                                //}
                            }
                        }
                        //foreach (ConfigNode cn in vessel_settings_node.nodes)
                        //{
                        //vessel_settings_node.RemoveNodes("");
                        //    if (debugging) Debug.Log("[CHATR] old nodes removed");
                        //}

                        vessel_settings_node = all_but_prev_vessel;
                        //if (debugging) Debug.Log("[CHATR] Update() :: vessel_settings node = all_but_prev_vessel");

                        new_vessel_node(prev_vessel);
                        //if (debugging) Debug.Log("[CHATR] Update() :: new node created using prev_vessel and added to vessel_settings node");

                        //save_vessel_settings_node();
                        vessel_settings_node.Save(settings_path + "vessels.cfg");
                        if (debugging) Debug.Log("[CHATR] Update() :: vessel_settings node saved to vessel_settings.cfg");


                        load_vessel_settings_node();    //reload with current vessel settings
                        search_vessel_settings_node();  //search for current vessel
                    }




                    vessel_prev_sit = vessel.situation;
                    vessel_prev_stage = vessel.currentStage;
                    //don't update vessel_part_count here!

                    if (vessel != prev_vessel && prev_vessel.vesselType == VesselType.EVA && (vessel.vesselType == VesselType.Ship || vessel.vesselType == VesselType.Lander || vessel.vesselType == VesselType.Station || vessel.vesselType == VesselType.Base))
                    {
                        if (aae_airlock_exist)
                        {
                            aae_airlock.Play();
                            if (debugging) Debug.Log("[CHATR] Returning from EVA, playing Airlock sound...");
                        }

                    }
                    
                    // prev_vessel = vessel;


                    //airlock sound
                    //todo fix airlock sound here
                    //sound plays after naut is already outside
                    if (vessel != prev_vessel && vessel.vesselType == VesselType.EVA && (prev_vessel.vesselType == VesselType.Ship || prev_vessel.vesselType == VesselType.Lander || prev_vessel.vesselType == VesselType.Station || prev_vessel.vesselType == VesselType.Base))
                    {
                        if (aae_airlock_exist)
                        {
                            aae_airlock.Play();
                            if (debugging) Debug.Log("[CHATR] Going on EVA, playing Airlock sound...");
                        }
                        
                    }

                    prev_vessel = vessel;
                }

                if (gui_running == false) start_GUI();
                
                //update remotetech info if needed
                if (remotetech_toggle)
                {
                    rt_update_timer += Time.deltaTime;
                    if (rt_update_timer > 2f)
                    {
                        updateRemoteTechData();
                        rt_update_timer = 0;
                    }
                }

                //consume_resources();    //try to use a little ElectricCharge


                ///////////////////////
                ///////////////////////

                //Do AAE

                //if (AAE_exists)
                //{

                //BACKGROUND
                if (aae_backgrounds_exist)
                {
                    //if EVA, stop background audio
                    if (vessel.vesselType == VesselType.EVA)
                    {
                        foreach (BackgroundSource src in backgroundsource_list)
                        {
                            src.audiosource.Stop();
                        }
                    }
                    else
                    {
                        //else play background audio

                        foreach (BackgroundSource src in backgroundsource_list)
                        {
                            if (src.audiosource.isPlaying == false)
                            {
                                src.audiosource.loop = true;
                                src.audiosource.Play();
                            }
                        }
                    }
                }

                //SOUNDSCAPE
                if (aae_soundscapes_exist)
                {
                    if (aae_soundscape_freq == 0)
                    {
                        //turned off
                        aae_soundscape.Stop();
                    }
                    else if (aae_soundscape_freq == 4)
                    {
                        //don't play soundscapes when within kerbin atmo
                        if (vessel.mainBody.bodyName != "Kerbin" || (vessel.mainBody.bodyName == "Kerbin" && (vessel.situation == Vessel.Situations.ORBITING || vessel.situation == Vessel.Situations.ESCAPING)))
                        {
                            //continuous loop of clips
                            if (aae_soundscape.isPlaying == false)
                            {
                                if (debugging) Debug.Log("[CHATR] playing next soundscape clip in continuous loop...");
                                set_soundscape_clip();
                                aae_soundscape.Play();
                            }
                        }
                    }
                    else
                    {
                        //don't play soundscapes when within kerbin atmo
                        if (vessel.mainBody.bodyName != "Kerbin" || (vessel.mainBody.bodyName == "Kerbin" && (vessel.situation == Vessel.Situations.ORBITING || vessel.situation == Vessel.Situations.ESCAPING)))
                        {
                            //run timer until timer_limit is reached, then play a random clip
                            if (aae_soundscape.isPlaying == false)
                            {
                                aae_soundscape_timer += Time.deltaTime;
                                if (aae_soundscape_timer > aae_soundscape_timer_limit)
                                {
                                    if (debugging) Debug.Log("[CHATR] soundscape1 timer limit reached, playing next clip...");
                                    set_soundscape_clip();
                                    aae_soundscape.Play();
                                    aae_soundscape_timer = 0;   //reset timer
                                    new_soundscape_loose_timer_limit(); //roll new timer limit
                                }
                            }
                        }
                    }
                }

                //EVA BREATHING
                if (aae_breathing_exist)
                {
                    if (vessel.vesselType == VesselType.EVA && aae_breathing.isPlaying == false)
                    {
                        if (debugging) Debug.Log("[CHATR] breathingsource.Play() loop has started");
                        aae_breathing.Play();
                    }
                    if (vessel.vesselType != VesselType.EVA && aae_breathing.isPlaying)
                    {
                        if (debugging) Debug.Log("[CHATR] No longer EVA, breathingsource.Stop()");
                        aae_breathing.Stop();
                    }
                }

                //WIND
                if (aae_wind_exist)
                {
                    //check that body has atmosphere, vessel is within it
                    if (vessel.mainBody.atmosphere && vessel.altitude < vessel.mainBody.maxAtmosphereAltitude)
                    {
                        //set volume according to atmosphere density
                        aae_wind.volume = aae_wind_vol_slider * Math.Min((float)vessel.atmDensity, 1);
                        //play the audio if not playing already
                        if (aae_wind.isPlaying == false)
                        {
                            if (debugging) Debug.Log("[CHATR] aae_wind.Play()");
                            aae_wind.loop = true;
                            aae_wind.Play();
                        }
                    }
                    else
                    {
                        //stop when out of atmosphere
                        if (aae_wind.isPlaying)
                        {
                            if (debugging) Debug.Log("[CHATR] aae_wind.Stop()");
                            if (aae_wind.isPlaying) aae_wind.Stop();
                        }
                    }
                }


                //add the suspenseful music track on loop
                //conditions?
                //vessel.situation == suborbital, true alt <= 10000m, descent speed > 10m/s
                if (vessel.situation == Vessel.Situations.SUB_ORBITAL && vessel.heightFromTerrain < 10000f && vessel.verticalSpeed < -10f)
                {
                    //start suspense loop
                    //todo add suspense loop
                    //landingsource.loop = true;
                    //landingsource.Play();
                }

                //}



                //END AAE
                /////////////////////////////////////////////
                /////////////////////////////////////////////
                //START SSTV


                //do SSTV
                if (sstv_exists)
                {
                    //insta-sstv activated
                    if (insta_sstv_key_just_changed == false && Input.GetKeyDown(insta_sstv_key) && sstv.isPlaying == false)
                    {
                        if (debugging) Debug.Log("[CHATR] beginning exchange,insta-SSTV");
                        if (exchange_playing)
                        {
                            //stop and reset any currently playing chatter
                            exchange_playing = false;
                            initial_chatter.Stop();
                            response_chatter.Stop();
                            initialize_new_exchange();
                        }
                        if (all_sstv_clips.Count > 0)
                        {
                            //get new clip, play it, set and get timers
                            sstv.clip = all_sstv_clips[rand.Next(0, all_sstv_clips.Count)];
                            sstv.Play();
                            sstv_timer = 0;
                            new_sstv_loose_timer_limit();
                        }
                        else Debug.LogWarning("[CHATR] No SSTV clips to play");
                    }

                    //timed sstv
                    if (all_sstv_clips.Count > 0)
                    {
                        //if clips exist, do things
                        if (sstv_freq > 0)
                        {
                            sstv_timer += Time.deltaTime;
                            if (sstv_timer > sstv_timer_limit)
                            {
                                sstv_timer = 0;
                                new_sstv_loose_timer_limit();
                                if (sstv.isPlaying == false)
                                {

                                    //get a random one and play
                                    if (exchange_playing)
                                    {
                                        //stop and reset any currently playing chatter
                                        exchange_playing = false;
                                        initial_chatter.Stop();
                                        response_chatter.Stop();
                                        initialize_new_exchange();
                                    }
                                    sstv.clip = all_sstv_clips[rand.Next(0, all_sstv_clips.Count)];
                                    sstv.Play();
                                    //sstv_timer = 0;
                                    //new_sstv_loose_timer_limit();
                                }
                            }
                        }
                    }
                }

                //END SSTV
                /////////////////////////////////////////////
                /////////////////////////////////////////////
                //START BEEPS

                //do beeps
                if (beeps_exists)
                {
                    if (dict_probe_samples.Count > 0 && OTP_playing == false)   //don't do any beeps here while OTP is playing
                    {
                        foreach (BeepSource bm in beepsource_list)
                        {
                            if (bm.precise)
                            {
                                //precise beeps
                                if (bm.precise_freq == -1)
                                {
                                    //beeps turned off at slider
                                    //bm.audiosource.Stop();    //squashed bug: this was breaking the one-time play button
                                    bm.audiosource.loop = false; ;  //instead of Stop(), just turn loop off in case it's on
                                }
                                else if (bm.precise_freq == 0)
                                {
                                    //looped beeps

                                    //disable looped sounds during chatter
                                    if ((disable_beeps_during_chatter && (initial_chatter.isPlaying || response_chatter.isPlaying)) || sstv.isPlaying)
                                    {
                                        bm.audiosource.Stop();
                                    }
                                    else
                                    {
                                        bm.audiosource.loop = true;
                                        if (bm.audiosource.isPlaying == false)
                                        {
                                            bm.audiosource.Play();
                                            SetAppLauncherButtonTexture(chatterer_button_SSTV);
                                        }
                                    }
                                }
                                else
                                {
                                    //timed beeps
                                    if (bm.audiosource.loop)
                                    {
                                        //if looping stop playing and set loop to off
                                        bm.audiosource.Stop();
                                        bm.audiosource.loop = false;
                                    }
                                    //then check the time
                                    bm.timer += Time.deltaTime;
                                    if (bm.timer > bm.precise_freq)
                                    {
                                        bm.timer = 0;
                                        //randomize beep if set to random (0)
                                        if (bm.current_clip == "Random")
                                        {
                                            //bm.audiosource.clip = all_beep_clips[rand.Next(0, all_beep_clips.Count)];
                                            set_beep_clip(bm);
                                        }
                                        //play beep unless disable == true && exchange_playing == true
                                        if (sstv.isPlaying || ((initial_chatter.isPlaying || response_chatter.isPlaying) && disable_beeps_during_chatter)) return;   //no beep under these conditions
                                        //if (disable_beeps_during_chatter == false || (disable_beeps_during_chatter == true && exchange_playing == false))
                                        else
                                        {
                                            //if (debugging) Debug.Log("[CHATR] timer limit reached, playing source " + bm.beep_name);
                                            bm.audiosource.Play();  //else beep
                                            SetAppLauncherButtonTexture(chatterer_button_SSTV);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                //imprecise beeps
                                //
                                //
                                //
                                //play a beep
                                //roll a new loose limit
                                if (bm.loose_freq == 0)
                                {
                                    //beeps turned off at slider
                                    //bm.audiosource.Stop();    //squashed bug: this was breaking the one-time play button
                                    bm.audiosource.loop = false; ;  //instead of Stop(), just turn loop off in case it's on
                                }
                                else
                                {
                                    bm.timer += Time.deltaTime;
                                    if (bm.timer > bm.loose_timer_limit)
                                    {
                                        bm.timer = 0;   //reset timer
                                        new_beep_loose_timer_limit(bm);    //set a new loose limit
                                        //randomize beep if set to random (0)
                                        if (bm.current_clip == "Random")
                                        {
                                            //bm.audiosource.clip = all_beep_clips[rand.Next(0, all_beep_clips.Count)];
                                            set_beep_clip(bm);
                                        }
                                        if (sstv.isPlaying || ((initial_chatter.isPlaying || response_chatter.isPlaying) && disable_beeps_during_chatter)) return;   //no beep under these conditions
                                        //if (disable_beeps_during_chatter == false || (disable_beeps_during_chatter == true && exchange_playing == false) || sstv.isPlaying == false)
                                        else
                                        {
                                            bm.audiosource.Play();  //else beep
                                            SetAppLauncherButtonTexture(chatterer_button_SSTV);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                //END BEEPS
                /////////////////////////////////////////////
                /////////////////////////////////////////////
                //START CHATTER

                //do chatter
                if (chatter_exists)
                {
                    if (vessel.GetCrewCount() > 0)
                    {
                        //Has crew onboard
                        //do insta-chatter if chatter is off
                        if (insta_chatter_key_just_changed == false && Input.GetKeyDown(insta_chatter_key) && exchange_playing == false && sstv.isPlaying == false)
                        {
                            //no chatter or sstv playing, play insta-chatter
                            if (debugging) Debug.Log("[CHATR] beginning exchange,insta-chatter");
                            begin_exchange(0);
                        }

                        //exchange_playing check added because insta-chatter response was blocked when chatter is turned off
                        if (chatter_freq > 0 || exchange_playing)
                        {
                            //Chatter is on
                            //consume_resources();
                            if (exchange_playing)
                            {
                                //Exchange in progress
                                if (initial_chatter.isPlaying == false)
                                {
                                    //initial chatter has finished playing
                                    //wait some seconds and respond
                                    secs_since_initial_chatter += Time.deltaTime;
                                    if (secs_since_initial_chatter > response_delay_secs)
                                    {
                                        //if (debugging) Debug.Log("[CHATR] response delay has elapsed...");
                                        if (response_chatter.isPlaying == false)
                                        {
                                            //play response clip if not already playing
                                            //print("response not currently playing...");

                                            if (response_chatter_started)
                                            {
                                                //has started flag is tripped but no chatter playing
                                                //response has ended
                                                if (debugging) Debug.Log("[CHATR] response has finished");
                                                exchange_playing = false;
                                                response_chatter_started = false;
                                                return;
                                            }

                                            if (response_chatter_set.Count > 0)
                                            {
                                                if (debugging) Debug.Log("[CHATR] playing response");
                                                response_chatter_started = true;
                                                if (initial_chatter_source == 1 && quindar_toggle)
                                                {
                                                    quindar1.Play();
                                                    //print("playing response first quindar");
                                                    response_chatter.PlayDelayed(quindar1.clip.length);
                                                    //print("playing response chatter");
                                                    quindar2.PlayDelayed(quindar1.clip.length + response_chatter.clip.length);
                                                    //print("playing response second quindar");
                                                }
                                                else response_chatter.Play();
                                            }
                                            else
                                            {
                                                if (debugging) Debug.LogWarning("[CHATR] response_chatter_set has no audioclips, abandoning exchange");
                                                exchange_playing = false;   //exchange is over
                                            }
                                            //print("playing response chatter...");
                                            //exchange_playing = false;   //exchange is over
                                        }
                                    }
                                }
                            }
                            else
                            {
                                //No exchange currently playing
                                secs_since_last_exchange += Time.deltaTime;

                                if (secs_since_last_exchange > secs_between_exchanges && sstv.isPlaying == false)
                                {
                                    if (debugging) Debug.Log("[CHATR] beginning exchange,auto");
                                    begin_exchange(0);
                                }

                                if (vessel.parts.Count != vessel_part_count || vessel_prev_stage != vessel.currentStage && sstv.isPlaying == false)
                                {
                                    //IMPROVE this so it doesn't chatter every vessel switch
                                    //part count or stage has changed
                                    if (debugging) Debug.Log("[CHATR] beginning exchange,parts/staging");
                                    pod_begins_exchange = true;
                                    begin_exchange(rand.Next(0, 3));  //delay Play for 0-2 seconds for randomness
                                }

                                if (vessel.vesselType != VesselType.EVA && vessel.situation != vessel_prev_sit && sstv.isPlaying == false)
                                {
                                    //situation (lander, orbiting, etc) has changed
                                    if (debugging) Debug.Log("[CHATR] beginning exchange,event::prev = " + vessel_prev_sit + " ::new = " + vessel.situation.ToString());
                                    pod_begins_exchange = true;
                                    begin_exchange(rand.Next(0, 3));  //delay Play for 0-2 seconds for randomness
                                }
                            }
                        }
                    }
                }


                vessel_prev_sit = vessel.situation;
                vessel_prev_stage = vessel.currentStage;
                vessel_part_count = vessel.parts.Count;

            }
            else
            {
                //FlightGlobals.ActiveVessel is null
                if (gui_running) stop_GUI();
            }
        }

        //private void consume_resources()
        //{
        //    if (TimeWarp.deltaTime == 0) return;    //do nothing if paused
        //    if (vessel.vesselType == VesselType.EVA || disable_power_usage) power_available = true;    //power always available when EVA
        //    else if (chatter_freq > 0 || sstv_freq > 0 || (beepsource_list[0].precise && beepsource_list[0].precise_freq > -1) || (beepsource_list[1].precise && beepsource_list[1].precise_freq > -1) || (beepsource_list[2].precise && beepsource_list[2].precise_freq > -1) || (beepsource_list[0].precise == false && beepsource_list[0].loose_freq > 0) || (beepsource_list[1].precise == false && beepsource_list[1].loose_freq > 0) || (beepsource_list[2].precise == false && beepsource_list[2].loose_freq > 0))
        //    {
        //        //else if anything is set to play a sound at some time, request ElectricCharge to determine power availability
        //        float recvd_amount = vessel.rootPart.RequestResource("ElectricCharge", 0.01f * TimeWarp.fixedDeltaTime);
        //        if (recvd_amount > 0) power_available = true;    // doesn't always send 100% of demand so as long as it sends something
        //        else power_available = false;
        //    }
        //}

    }
}
