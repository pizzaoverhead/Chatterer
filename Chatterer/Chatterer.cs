///////////////////////////////////////////////////////////////////////////////
//
//    Chatterer for Kerbal Space Program
//    Copyright (C) 2013 Iannic-ann-od
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
 * 
 * ADD EVA-capsule chatter (if nearby crew > 0) and capsule-capsule chatter (if vessel crew > 1)
 * ADD user-defined # of beep sources
 * ADD 3-5 built-in audio settings storage (chatterer.cfg)
 * ADD persistent per-ship audio settings storage (persistence.sfs)
 * 
 * 
 * 
 * 
 * [0.5]
 * ADDED chatter, beep, and sstv search max to settings read/write
 * ADDED SSTV audio
 * ADDED precision beeps/random beeps switch
 * ADDED toggle for Check for Updates (http)
 * ADDED toggle to disable beeps during chatter
 * ADDED more beeps
 * ADDED random and looped beeps
 * CHANGED gui
 * FIXED not all skins always working
 * ADDED icon position can be changed
 * FIXED docking auto-chatter
 * FIXED exchange_playing is turned back to false as soon as response starts.  fix it to flip false after response finishes
 * CHANGED beep pitch to a % like everything else
 * FIXED shortened directory structure
 * FIXED Load buttin to not load already loaded dirs and not accept 'directory name'
 * FIXED ugly read/write settings
 * 
 */


///////////////////////////////////////////////////////////////////////////////


using System;
using System.Collections.Generic;
using UnityEngine;

namespace RBR
{
    public class RBRAudioList
    {
        //class to manage chatter clips
        public List<AudioClip> capcom;
        public List<AudioClip> capsule;
        public string directory;
        public bool is_active;

        public RBRAudioList()
        {
            capcom = new List<AudioClip>();
            capsule = new List<AudioClip>();
            directory = "dir";
            is_active = true;
        }
    }

    public class RBRBeepManager
    {
        //class to manage beeps
        public AudioSource source;
        public bool precise;
        public float precise_freq_slider;
        public int precise_freq;
        public int prev_precise_freq;
        public float loose_freq_slider;
        public int loose_freq;
        public int prev_loose_freq;
        public int loose_timer_limit;
        public float vol_slider;
        public float prev_vol_slider;
        public float pitch_slider;
        public float prev_pitch_slider;
        public float timer;
        public int current_clip;

        public RBRBeepManager()
        {
            precise = true;
            source = new AudioSource();
            precise_freq_slider = -1f;
            precise_freq = -1;
            prev_precise_freq = -1;
            loose_freq_slider = 0;
            loose_freq = 0;
            prev_loose_freq = 0;
            loose_timer_limit = 0;
            vol_slider = 0.3f;
            prev_vol_slider = 0.3f;
            pitch_slider = 1f;
            prev_pitch_slider = 1f;
            timer = 0;
            current_clip = 1;
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class chatterer : MonoBehaviour
    {
        private bool debugging = false;      //lots of extra log info if true

        //Max beeps and chatter to load from each folder
        private int chatter_search_max = 50;    //max capcom and capsule chatter to search for
        private int beep_search_max = 20;       //max # of beep clips to search for
        private int sstv_search_max = 20;       //max # of sstv clips to search for

        private static System.Random rand = new System.Random();
        private Vessel vessel;          //is set to FlightGlobals.ActiveVessel
        private Vessel prev_vessel;     //to detect change in active vessel

        //Audio
        private GameObject audioplayer = new GameObject();          //AudioSources are added to this
        private AudioSource initial_chatter = new AudioSource();
        private AudioSource response_chatter = new AudioSource();
        private AudioSource quindar1 = new AudioSource();
        private AudioSource quindar2 = new AudioSource();
        private AudioSource sstv = new AudioSource();

        private List<RBRBeepManager> beep_manager = new List<RBRBeepManager>();     //attay to hold the beep sources and settings for individual beeps

        private List<RBRAudioList> chatter_array = new List<RBRAudioList>();        //array of all chatter clips and some settings
        private List<AudioClip> all_beep_clips = new List<AudioClip>();             //list of all beep clips
        private List<AudioClip> all_sstv_clips = new List<AudioClip>();             //list of all sstv clips

        private List<AudioClip> current_capcom_chatter = new List<AudioClip>();     //holds chatter of toggled sets
        private List<AudioClip> current_capsule_chatter = new List<AudioClip>();    //one of these becomes initial, the other response
        private int current_capcom_clip;
        private int current_capsule_clip;

        private bool exchange_playing = false;
        private bool response_chatter_started = false;
        private bool pod_begins_exchange = false;
        private int initial_chatter_source; //whether capsule or capcom begins exchange
        private List<AudioClip> initial_chatter_set = new List<AudioClip>();    //random clip pulled from here
        private int initial_chatter_index;  //index of random clip
        private List<AudioClip> response_chatter_set = new List<AudioClip>();   //and here
        private int response_chatter_index;
        private int response_delay_secs;

        //Window, icon, and textures
        protected Rect window_0_pos = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
        protected Rect ui_icon_pos;
        private Texture2D ui_icon_off = new Texture2D(30, 30, TextureFormat.ARGB32, false);
        private Texture2D ui_icon_on = new Texture2D(30, 30, TextureFormat.ARGB32, false);
        private Texture2D ui_icon = new Texture2D(30, 30, TextureFormat.ARGB32, false);
        private Texture2D line_512x4 = new Texture2D(512, 8, TextureFormat.ARGB32, false);
        private bool ui_icons_loaded = true;

        //GUI styles
        private bool gui_styles_set = false;
        private GUIStyle label_txt_left;
        private GUIStyle label_txt_center;
        private GUIStyle label_txt_right;
        private GUIStyle label_txt_red_center;
        private GUIStyle button_txt_right;
        private GUIStyle button_txt_center;
        private GUIStyle button_txt_center_green;
        private GUIStyle gs_menu_sliders = new GUIStyle();  //Section labels change between btn_center and btn_center_green
        private GUIStyle gs_menu_audiosets = new GUIStyle();
        private GUIStyle gs_menu_remotetech = new GUIStyle();
        private GUIStyle gs_menu_settings = new GUIStyle();
        private GUIStyle gs_beep1 = new GUIStyle();
        private GUIStyle gs_beep2 = new GUIStyle();
        private GUIStyle gs_beep3 = new GUIStyle();

        //GUI things
        private bool power_available = true;
        private bool run_once = true;   //used to run some things just once in Update() that don't get done in Awake()
        private bool gui_running = false;
        private string active_menu = "sliders";
        private bool main_gui_minimized = false;
        private int gui_style = 2;  //default gui style
        private bool quindar_toggle = true;
        private bool disable_beeps_during_chatter = true;
        private bool remotetech_toggle = false;
        private bool misc_show_skins = false;
        private bool http_update_check = false;
        private bool changing_icon_pos = false;
        private string custom_dir_name = "directory name";
        //private int total_beep_sources = 3;     //# of beep audiosources available    //not yet
        private int sel_beep_src = 0;   //currently selected beep source

        //Counters
        private float cfg_update_timer = 0;
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
        private float chatter_vol_slider = 1f;
        private float prev_chatter_vol_slider = 1f;

        private float quindar_vol_slider = 0.5f;
        private float prev_quindar_vol_slider = 0.5f;

        private float sstv_freq_slider = 1;
        private int sstv_freq = 1;
        private int prev_sstv_freq = 1;
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
        private string this_version = "0.5";
        private string latest_version = "";
        private bool recvd_latest_version = false;


        //////////


        private void set_gui_styles()
        {
            label_txt_left = new GUIStyle(GUI.skin.label);
            label_txt_left.normal.textColor = Color.white;
            label_txt_left.alignment = TextAnchor.MiddleLeft;

            label_txt_center = new GUIStyle(GUI.skin.label);
            label_txt_center.normal.textColor = Color.white;
            label_txt_center.alignment = TextAnchor.MiddleCenter;

            label_txt_right = new GUIStyle(GUI.skin.label);
            label_txt_right.normal.textColor = Color.white;
            label_txt_right.alignment = TextAnchor.MiddleRight;

            label_txt_red_center = new GUIStyle(GUI.skin.label);
            label_txt_red_center.normal.textColor = Color.red;
            label_txt_red_center.alignment = TextAnchor.MiddleCenter;

            button_txt_right = new GUIStyle(GUI.skin.button);
            button_txt_right.normal.textColor = Color.white;
            button_txt_right.alignment = TextAnchor.MiddleRight;

            button_txt_center = new GUIStyle(GUI.skin.button);
            button_txt_center.normal.textColor = Color.white;
            button_txt_center.alignment = TextAnchor.MiddleCenter;

            button_txt_center_green = new GUIStyle(GUI.skin.button);
            button_txt_center_green.normal.textColor = button_txt_center_green.hover.textColor = button_txt_center_green.active.textColor = button_txt_center_green.focused.textColor = Color.green;
            button_txt_center_green.alignment = TextAnchor.MiddleCenter;

            reset_menu_gs();
            if (active_menu == "sliders") gs_menu_sliders = button_txt_center_green;
            if (active_menu == "audiosets") gs_menu_audiosets = button_txt_center_green;
            if (active_menu == "remotetech") gs_menu_remotetech = button_txt_center_green;
            if (active_menu == "settings") gs_menu_settings = button_txt_center_green;

            reset_beep_gs();
            gs_beep1 = button_txt_center_green;

            gui_styles_set = true;
            if (debugging) Debug.Log("[CHATR] GUI styles set");
        }

        private void reset_menu_gs()
        {
            gs_menu_sliders = button_txt_center;
            gs_menu_audiosets = button_txt_center;
            gs_menu_remotetech = button_txt_center;
            gs_menu_settings = button_txt_center;
        }

        private void reset_beep_gs()
        {
            gs_beep1 = button_txt_center;
            gs_beep2 = button_txt_center;
            gs_beep3 = button_txt_center;
        }

        private void consume_resources()
        {
            if (TimeWarp.deltaTime == 0) return;    //do nothing if paused
            if (vessel.vesselType == VesselType.EVA) power_available = true;    //power always available when EVA
            else if (chatter_freq > 0 || sstv_freq > 0 || (beep_manager[0].precise && beep_manager[0].precise_freq > -1) || (beep_manager[1].precise && beep_manager[1].precise_freq > -1) || (beep_manager[2].precise && beep_manager[2].precise_freq > -1) || (beep_manager[0].precise == false && beep_manager[0].loose_freq > 0) || (beep_manager[1].precise == false && beep_manager[1].loose_freq > 0) || (beep_manager[2].precise == false && beep_manager[2].loose_freq > 0))
            {
                //else if anything is set to play a sound at some time, request ElectricCharge to determine power availability
                float recvd_amount = vessel.rootPart.RequestResource("ElectricCharge", 0.01f * TimeWarp.deltaTime);
                if (recvd_amount > 0) power_available = true;    // doesn't always send 100% of demand so as long as it sends something
                else power_available = false;
            }
        }

        private void start_GUI()
        {
            load_settings();
            RenderingManager.AddToPostDrawQueue(3, new Callback(draw_GUI));	//start the GUI
            gui_running = true;
        }

        private void stop_GUI()
        {
            RenderingManager.RemoveFromPostDrawQueue(3, new Callback(draw_GUI));	//stop the GUI
            gui_running = false;
        }

        private void write_settings()
        {
            string settings = "";
            settings += "# number of chatter clips to try to load for EACH\n";
            settings += "# of the two chatter types: capcom and capsule\n";
            settings += "CHATTER SEARCH MAX = " + chatter_search_max + "\n";
            settings += "# number of beep clips to try to load\n";
            settings += "BEEP SEARCH MAX = " + beep_search_max + "\n";
            settings += "# number of sstv clips to try to load\n";
            settings += "SSTV SEARCH MAX = " + sstv_search_max + "\n\n";
            settings += "MAIN WINDOW POS = " + window_0_pos.x + "," + window_0_pos.y + "\n";
            settings += "UI ICON POS = " + ui_icon_pos.x + "," + ui_icon_pos.y + "\n";
            settings += "MAIN GUI MINIMIZED = " + main_gui_minimized + "\n";
            settings += "ACTIVE MENU = " + active_menu + "\n";
            settings += "INSTA-CHATTER KEY = " + insta_chatter_key + "\n";
            settings += "GUI STYLE = " + gui_style + "\n";
            settings += "HTTP UPDATE CHECK = " + http_update_check + "\n";
            settings += "REMOTETECH INTEGRATION = " + remotetech_toggle + "\n";
            settings += "CHATTER FREQUENCY = " + chatter_freq + "\n";
            settings += "CHATTER VOLUME = " + chatter_vol_slider.ToString("F2") + "\n";
            settings += "QUINDAR TOGGLE = " + quindar_toggle + "\n";
            settings += "QUINDAR VOLUME = " + quindar_vol_slider.ToString("F2") + "\n";
            settings += "BEEP 1 SETTINGS = " + beep_manager[0].precise + "," + beep_manager[0].precise_freq + "," + beep_manager[0].loose_freq + "," + beep_manager[0].vol_slider.ToString("F2") + "," + beep_manager[0].pitch_slider.ToString("F2") + "," + beep_manager[0].current_clip + "\n";
            settings += "BEEP 2 SETTINGS = " + beep_manager[1].precise + "," + beep_manager[1].precise_freq + "," + beep_manager[1].loose_freq + "," + beep_manager[1].vol_slider.ToString("F2") + "," + beep_manager[1].pitch_slider.ToString("F2") + "," + beep_manager[1].current_clip + "\n";
            settings += "BEEP 3 SETTINGS = " + beep_manager[2].precise + "," + beep_manager[2].precise_freq + "," + beep_manager[2].loose_freq + "," + beep_manager[2].vol_slider.ToString("F2") + "," + beep_manager[2].pitch_slider.ToString("F2") + "," + beep_manager[2].current_clip + "\n";
            settings += "SELECTED BEEP SOURCE = " + sel_beep_src + "\n";
            settings += "SSTV FREQUENCY = " + sstv_freq + "\n";
            settings += "SSTV VOLUME = " + sstv_vol_slider.ToString("F2") + "\n";
            settings += "INSTA-SSTV KEY = " + insta_sstv_key + "\n";
            KSP.IO.File.WriteAllText<chatterer>(settings, "chatterer.cfg", null);
        }

        private void load_settings()
        {
            if (KSP.IO.File.Exists<chatterer>("chatterer.cfg", null))
            {
                string[] data = KSP.IO.File.ReadAllLines<chatterer>("chatterer.cfg", null);
                string[] name_val;
                string name = "";
                string val = "";

                foreach (string s in data)
                {

                    name_val = s.Split('=');
                    if (name_val.Length > 1)
                    {
                        //expected config line with two elements
                        name = name_val[0].Trim();
                        val = name_val[1].Trim();

                        if (val != "")
                        {
                            if (name == "CHATTER SEARCH MAX") chatter_search_max = Convert.ToInt32(val);
                            if (name == "BEEP SEARCH MAX") beep_search_max = Convert.ToInt32(val);
                            if (name == "SSTV SEARCH MAX") sstv_search_max = Convert.ToInt32(val);
                            if (name == "MAIN WINDOW POS")
                            {
                                string[] temp = val.Split(',');
                                window_0_pos = new Rect(Convert.ToSingle(temp[0].Trim()), Convert.ToSingle(temp[1].Trim()), 10f, 10f);
                            }
                            if (name == "UI ICON POS")
                            {
                                string[] temp = val.Split(',');
                                ui_icon_pos = new Rect(Convert.ToSingle(temp[0].Trim()), Convert.ToSingle(temp[1].Trim()), 30f, 30f);
                            }
                            if (name == "MAIN GUI MINIMIZED") main_gui_minimized = Convert.ToBoolean(val);
                            if (name == "ACTIVE MENU") active_menu = val.ToLower();
                            if (name == "INSTA-CHATTER KEY") insta_chatter_key = (KeyCode)Enum.Parse(typeof(KeyCode), val);
                            if (name == "GUI STYLE") gui_style = Convert.ToInt32(val);
                            if (name == "HTTP UPDATE CHECK") http_update_check = Convert.ToBoolean(val);
                            if (name == "REMOTETECH INTEGRATION") remotetech_toggle = Convert.ToBoolean(val);
                            if (name == "CHATTER FREQUENCY")
                            {
                                chatter_freq = Convert.ToInt32(val);
                                chatter_freq_slider = Convert.ToSingle(val);
                                prev_chatter_freq = chatter_freq;
                            }
                            if (name == "CHATTER VOLUME")
                            {
                                chatter_vol_slider = Convert.ToSingle(val);
                                prev_chatter_vol_slider = chatter_vol_slider;
                            }
                            if (name == "QUINDAR TOGGLE") quindar_toggle = Convert.ToBoolean(val);
                            if (name == "QUINDAR VOLUME")
                            {
                                quindar_vol_slider = Convert.ToSingle(val);
                                prev_quindar_vol_slider = quindar_vol_slider;
                            }
                            if (name == "BEEP 1 SETTINGS")
                            {
                                string[] temp = val.Split(',');
                                load_beep_slider_settings(0, temp);
                            }
                            if (name == "BEEP 2 SETTINGS")
                            {
                                string[] temp = val.Split(',');
                                load_beep_slider_settings(1, temp);
                            }
                            if (name == "BEEP 3 SETTINGS")
                            {
                                string[] temp = val.Split(',');
                                load_beep_slider_settings(2, temp);
                            }
                            if (name == "SELECTED BEEP SOURCE") sel_beep_src = Convert.ToInt32(val);
                            if (name == "SSTV FREQUENCY")
                            {
                                sstv_freq = Convert.ToInt32(val);
                                sstv_freq_slider = Convert.ToSingle(val);
                                prev_sstv_freq = sstv_freq;
                            }
                            if (name == "SSTV VOLUME")
                            {
                                sstv_vol_slider = Convert.ToSingle(val);
                                prev_sstv_vol_slider = sstv_vol_slider;
                            }
                            if (name == "INSTA-SSTV KEY") insta_sstv_key = (KeyCode)Enum.Parse(typeof(KeyCode), val);
                        }
                    }
                }
            }
        }

        private void load_beep_slider_settings(int i, string[] vals)
        {
            beep_manager[i].precise = Convert.ToBoolean(vals[0]);

            beep_manager[i].precise_freq = Convert.ToInt32(vals[1]);
            beep_manager[i].precise_freq_slider = beep_manager[i].precise_freq;
            beep_manager[i].prev_precise_freq = beep_manager[i].precise_freq;

            beep_manager[i].loose_freq = Convert.ToInt32(vals[2]);
            beep_manager[i].loose_freq_slider = beep_manager[i].loose_freq;
            beep_manager[i].prev_loose_freq = beep_manager[i].loose_freq;

            beep_manager[i].vol_slider = Convert.ToSingle(vals[3]);
            beep_manager[i].prev_vol_slider = beep_manager[i].vol_slider;

            beep_manager[i].pitch_slider = Convert.ToSingle(vals[4]);
            beep_manager[i].prev_pitch_slider = beep_manager[i].pitch_slider;

            beep_manager[i].current_clip = Convert.ToInt32(vals[5]);
        }

        private void load_icons()
        {
            string path_icon_on = "RBR/Textures/chatterer_icon_on";
            string path_icon_off = "RBR/Textures/chatterer_icon_off";

            if (GameDatabase.Instance.ExistsTexture(path_icon_on) && GameDatabase.Instance.ExistsTexture(path_icon_off))
            {
                //print("icon textures exist, loading...");
                ui_icon_on = GameDatabase.Instance.GetTexture(path_icon_on, false);
                ui_icon_off = GameDatabase.Instance.GetTexture(path_icon_off, false);
            }
            else
            {
                //print("texture doesn't exist: icon_on");
                ui_icons_loaded = false;
            }
            if (ui_icons_loaded)
            {
                //print("VOID::icon textures load OK");
                ui_icon_pos = new Rect((Screen.width / 2) - 285, Screen.height - 32, 30, 30);
                if (chatter_freq == 0) ui_icon = ui_icon_off;
                else ui_icon = ui_icon_on;
            }
            else
            {
                //print("VOID::icon textures load ERROR");
                ui_icon_pos = new Rect((Screen.width / 2) - 320, Screen.height - 22, 70, 20);
            }
        }

        private void get_latest_version()
        {
            bool got_all_info = false;

            WWWForm form = new WWWForm();
            form.AddField("version", this_version);

            WWW version = new WWW("http://rbri.co.nf/ksp/chatterer/get_latest_version.php", form.data);

            while (got_all_info == false)
            {
                if (version.isDone)
                {
                    latest_version = version.text;
                    got_all_info = true;
                }
            }
            recvd_latest_version = true;
            if (debugging) Debug.Log("[CHATR] recv'd latest version info: " + latest_version);
        }

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

        private void new_audiosets_cfg()
        {
            string sets = "";
            sets += "apollo11,True\nsts1,True\nrussian,True";
            KSP.IO.File.WriteAllText<chatterer>(sets, "audiosets.cfg", null);
        }

        private void read_audiosets_cfg()
        {
            string[] data = KSP.IO.File.ReadAllLines<chatterer>("audiosets.cfg", null);
            string[] data_split;

            int x = 0;
            foreach (string s in data)
            {
                if (s != null || s.Trim() != "")
                {
                    chatter_array.Add(new RBRAudioList());  //create a new entry in the array for each audioset

                    data_split = data[x].Split(',');
                    chatter_array[x].directory = data_split[0];
                    //chatter_array.Add(data_split[0]);
                    if (data_split.Length == 2) chatter_array[x].is_active = Convert.ToBoolean(data_split[1]);
                    //else chatter_array[x].is_active = true;
                    x++;
                }
            }
        }

        private void load_quindar()
        {
            //Create two AudioSources for quindar so PlayDelayed() can delay two beeps
            string path = "RBR/Sounds/quindar_01";

            if (GameDatabase.Instance.ExistsAudioClip(path))
            {
                //print("Quindar loaded OKAY " + path);
                quindar1.clip = GameDatabase.Instance.GetAudioClip(path);
                quindar2.clip = GameDatabase.Instance.GetAudioClip(path);
            }
            else Debug.LogWarning("[CHATR] Quindar audio load FAILED");
        }

        private void load_beep_clips()
        {
            string path;
            int i;

            for (i = 1; i <= beep_search_max; i++)
            {
                path = "RBR/Sounds/beep_" + i.ToString("D2");
                if (GameDatabase.Instance.ExistsAudioClip(path))
                {
                    all_beep_clips.Add(GameDatabase.Instance.GetAudioClip(path));
                    //print(path + " loaded OKAY");
                }
            }
            if (all_beep_clips.Count == 0) Debug.LogWarning("[CHATR] No beep clips found");
        }

        private void load_all_sstv_clips()
        {
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
            if (all_sstv_clips.Count == 0) Debug.LogWarning("[CHATR] No SSTV clips found");
        }

        private void load_all_chatter()
        {
            chatter_array.Clear();

            if (KSP.IO.File.Exists<chatterer>("audiosets.cfg", null) == false)
            {
                //file not found, create a new one
                Debug.LogWarning("[CHATR] audiosets.cfg is missing, recreating default");
                new_audiosets_cfg();
            }

            read_audiosets_cfg();       //initial read

            if (chatter_array.Count == 0)
            {
                //file was empty
                Debug.LogWarning("[CHATR] audiosets.cfg is empty, recreating default");
                new_audiosets_cfg();    //re-create default
                read_audiosets_cfg();   //re-read
            }

            string path;
            int k;
            int jebediah = 0;
            for (k = 0; k < chatter_array.Count; k++)
            {
                int i;
                for (i = 1; i <= chatter_search_max; i++)
                {
                    //attempt to load up to 50 capcom sound clips
                    path = "RBR/Sounds/" + chatter_array[k].directory + "/capcom_" + i.ToString("D2");
                    if (GameDatabase.Instance.ExistsAudioClip(path))
                    {
                        chatter_array[k].capcom.Add(GameDatabase.Instance.GetAudioClip(path));
                        jebediah++;
                    }
                }
                if (chatter_array[k].capcom.Count == 0) Debug.LogWarning("[CHATR] dir: " + chatter_array[k].directory + " has 0 capcom clips");

                for (i = 1; i <= chatter_search_max; i++)
                {
                    //attempt to load up to 50 capsule sound clips
                    path = "RBR/Sounds/" + chatter_array[k].directory + "/capsule_" + i.ToString("D2");
                    if (GameDatabase.Instance.ExistsAudioClip(path))
                    {
                        chatter_array[k].capsule.Add(GameDatabase.Instance.GetAudioClip(path));
                        jebediah++;
                    }
                }
                if (chatter_array[k].capsule.Count == 0) Debug.LogWarning("[CHATR] dir: " + chatter_array[k].directory + " has 0 capsule clips");
            }
            if (jebediah == 0) Debug.LogError("[CHATR] No chatter clips found to load!");
            load_toggled_sets();
        }

        private void load_toggled_sets()
        {
            if (debugging) Debug.Log("[CHATR] loading toggled sets...");
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
            string dirs = "";

            int x;
            for (x = 0; x < chatter_array.Count; x++)
            {
                dirs += chatter_array[x].directory + "," + chatter_array[x].is_active + "\n";
            }

            KSP.IO.File.WriteAllText<chatterer>(dirs, "audiosets.cfg", null);
            if (debugging) Debug.Log("[CHATR] toggled sets loaded OK");
        }

        private void new_beep_loose_timer_limit(RBRBeepManager bm)
        {
            if (bm.loose_freq == 1) bm.loose_timer_limit = rand.Next(120, 301);
            else if (bm.loose_freq == 2) bm.loose_timer_limit = rand.Next(60, 121);
            else if (bm.loose_freq == 3) bm.loose_timer_limit = rand.Next(30, 61);
            else if (bm.loose_freq == 4) bm.loose_timer_limit = rand.Next(15, 31);
            else if (bm.loose_freq == 5) bm.loose_timer_limit = rand.Next(5, 16);
            else if (bm.loose_freq == 6) bm.loose_timer_limit = rand.Next(1, 6);
            if (debugging) Debug.Log("[CHATR] new beep loose timer limit set: " + bm.loose_timer_limit);
        }

        private void new_sstv_loose_timer_limit()
        {
            if (sstv_freq == 1) sstv_timer_limit = rand.Next(1800, 3601);       //30-60mins
            else if (sstv_freq == 2) sstv_timer_limit = rand.Next(600, 1801);   //15-30m
            else if (sstv_freq == 3) sstv_timer_limit = rand.Next(300, 601);    //5-15m
            else if (sstv_freq == 4) sstv_timer_limit = rand.Next(120, 301);    //2-5m
            if (debugging) Debug.Log("[CHATR] new sstv timer limit set: " + sstv_timer_limit.ToString("F0"));
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
                foreach (RBRBeepManager bm in beep_manager)
                {
                    bm.source.Stop();
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
                foreach (RBRBeepManager bm in beep_manager)
                {
                    bm.source.loop = false;
                    bm.source.Stop();
                    bm.timer = 0;
                }
            }
        }

        private void chatter_sliders()
        {
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
            GUILayout.Label("Chatter frequency: " + chatter_freq_str, label_txt_left, GUILayout.ExpandWidth(true));
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

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Chatter volume: " + (chatter_vol_slider * 100).ToString("F0") + "%", label_txt_left, GUILayout.ExpandWidth(true));
            chatter_vol_slider = GUILayout.HorizontalSlider(chatter_vol_slider, 0, 1f, GUILayout.Width(140f));
            GUILayout.EndHorizontal();

            if (chatter_vol_slider != prev_chatter_vol_slider)
            {
                if (debugging) Debug.Log("[CHATR] Changing chatter AudioSource volume...");
                initial_chatter.volume = chatter_vol_slider;
                response_chatter.volume = chatter_vol_slider;
                prev_chatter_vol_slider = chatter_vol_slider;
            }


            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            quindar_toggle = GUILayout.Toggle(quindar_toggle, "Quindar tones");
            GUILayout.EndHorizontal();

            if (quindar_toggle)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("Quindar volume: " + (quindar_vol_slider * 100).ToString("F0") + "%", label_txt_left, GUILayout.ExpandWidth(true));
                quindar_vol_slider = GUILayout.HorizontalSlider(quindar_vol_slider, 0, 1f, GUILayout.Width(135f));
                GUILayout.EndHorizontal();

                if (quindar_vol_slider != prev_quindar_vol_slider)
                {
                    if (debugging) Debug.Log("[CHATR] Quindar volume has been changed...");
                    quindar1.volume = quindar_vol_slider;
                    quindar2.volume = quindar_vol_slider;
                    prev_quindar_vol_slider = quindar_vol_slider;
                }
            }
        }

        private void sstv_sliders()
        {
            if (all_sstv_clips.Count > 0)
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

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("SSTV frequency: " + sstv_freq_str, label_txt_left, GUILayout.ExpandWidth(true));
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

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("SSTV volume: " + (sstv_vol_slider * 100).ToString("F0") + "%", label_txt_left, GUILayout.ExpandWidth(true));
                sstv_vol_slider = GUILayout.HorizontalSlider(sstv_vol_slider, 0, 1f, GUILayout.Width(140f));
                GUILayout.EndHorizontal();

                if (sstv_vol_slider != prev_sstv_vol_slider)
                {
                    if (debugging) Debug.Log("[CHATR] Changing SSTV AudioSource volume...");
                    sstv.volume = sstv_vol_slider;
                    prev_sstv_vol_slider = sstv_vol_slider;
                }
            }
            else
            {
                //No sstv clips in list
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("\n-- SSTV DISABLED --\n\n-- NO SOUND FILES TO PLAY --", label_txt_red_center);
                GUILayout.EndHorizontal();
            }
        }

        private void beep_sliders()
        {
            if (all_beep_clips.Count > 0)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Beep #1", gs_beep1, GUILayout.ExpandWidth(true)))
                {
                    reset_beep_gs();
                    gs_beep1 = button_txt_center_green;
                    sel_beep_src = 0;
                }
                if (GUILayout.Button("Beep #2", gs_beep2, GUILayout.ExpandWidth(true)))
                {
                    reset_beep_gs();
                    gs_beep2 = button_txt_center_green;
                    sel_beep_src = 1;
                }
                if (GUILayout.Button("Beep #3", gs_beep3, GUILayout.ExpandWidth(true)))
                {
                    reset_beep_gs();
                    gs_beep3 = button_txt_center_green;
                    sel_beep_src = 2;
                }
                GUILayout.EndHorizontal();

                RBRBeepManager bm = beep_manager[sel_beep_src];

                string beep_timing_str = "Loose";
                if (bm.precise) beep_timing_str = "Precise";

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("Beep Timing: " + beep_timing_str, label_txt_left, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Change", button_txt_right, GUILayout.ExpandWidth(false)))
                {
                    //timing mode is being switched
                    bm.precise = !bm.precise;
                    bm.timer = 0;
                    bm.source.loop = false;
                    bm.source.Stop();

                    if (bm.precise)
                    {
                        if (bm.current_clip == 0 && bm.precise_freq == 0)
                        {
                            //disallow random looped clips
                            bm.current_clip = 1;
                            bm.source.clip = all_beep_clips[bm.current_clip - 1];
                        }
                        else if (bm.current_clip > 0) bm.source.clip = all_beep_clips[bm.current_clip - 1];
                    }
                    else new_beep_loose_timer_limit(bm);   //set new loose time limit

                }
                GUILayout.EndHorizontal();

                if (bm.precise)
                {
                    //show exact slider
                    bm.precise_freq = Convert.ToInt32(Math.Round(bm.precise_freq_slider));
                    string beep_freq_str = "";
                    if (bm.precise_freq == -1) beep_freq_str = "No beeps";
                    else if (bm.precise_freq == 0) beep_freq_str = "Loop";
                    else beep_freq_str = "Every " + bm.precise_freq.ToString() + "s";

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label("Beep frequency: " + beep_freq_str, label_txt_left, GUILayout.ExpandWidth(true));
                    bm.precise_freq_slider = GUILayout.HorizontalSlider(bm.precise_freq_slider, -1f, 60f, GUILayout.Width(100f));
                    GUILayout.EndHorizontal();

                    if (bm.precise_freq != bm.prev_precise_freq)
                    {
                        if (debugging) Debug.Log("[CHATR] precise_freq has changed, resetting beep_timer...");
                        bm.timer = 0;
                        bm.prev_precise_freq = bm.precise_freq;
                        if (bm.precise_freq == 0 && bm.current_clip == 0)
                        {
                            //frequency has changed to looped mode
                            //current clip == random
                            //not allowed, too silly
                            bm.current_clip = 1;
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

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label("Beep frequency: " + beep_freq_str, label_txt_left, GUILayout.ExpandWidth(true));
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

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("Beep volume: " + (bm.vol_slider * 100).ToString("F0") + "%", label_txt_left, GUILayout.ExpandWidth(true));
                bm.vol_slider = GUILayout.HorizontalSlider(bm.vol_slider, 0, 1f, GUILayout.Width(140f));
                GUILayout.EndHorizontal();

                if (bm.vol_slider != bm.prev_vol_slider)
                {
                    if (debugging) Debug.Log("[CHATR] Beep volume has been changed...");
                    bm.source.volume = bm.vol_slider;
                    bm.prev_vol_slider = bm.vol_slider;
                }

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("Beep pitch: " + (bm.pitch_slider * 100).ToString("F0") + "%", label_txt_left, GUILayout.ExpandWidth(true));
                bm.pitch_slider = GUILayout.HorizontalSlider(bm.pitch_slider, 0.1f, 5f, GUILayout.Width(140f));
                GUILayout.EndHorizontal();

                if (bm.pitch_slider != bm.prev_pitch_slider)
                {
                    if (debugging) Debug.Log("[CHATR] Beep pitch has been changed, changing pitch for all beeps...");
                    bm.source.pitch = bm.pitch_slider;
                    bm.prev_pitch_slider = bm.pitch_slider;
                }


                //click <-- allows random clip but gives a null ref the first time on index, then works fine thereafter
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                if (GUILayout.Button("<--", GUILayout.ExpandWidth(true)))
                {
                    bm.current_clip--;    //lower it
                    //after lowering
                    //if current_clip < 0
                    //can't have negatives : set to highest beep : current_clip = all_beeps.Count
                    //then set source.clip = current_clip - 1
                    if (bm.current_clip < 0 || (bm.current_clip == 0 && bm.precise && bm.precise_freq == 0))
                    {
                        bm.current_clip = all_beep_clips.Count;
                        bm.source.clip = all_beep_clips[bm.current_clip - 1];
                    }
                    else if (bm.current_clip > 0) bm.source.clip = all_beep_clips[bm.current_clip - 1];


                    //if current_clip == 0 (random)
                    // if precise == true && precise_freq == 0
                    // don't allow looped random.  too silly
                    // current_clip = all_beeps.Count
                    // then set source.clip = current_clip - 1
                    //
                    // if precise == true && precise_freq > 0 RANDOM OK
                    //
                    //
                    //
                    // if precise == false random ok
                    //else RANDOM OK






                    //if (beep_manager[sel_beep_src].current_clip < 0 || (beep_manager[sel_beep_src].current_clip == 0 && beep_manager[sel_beep_src].precise == true && beep_manager[sel_beep_src].precise_freq == 0)) beep_manager[sel_beep_src].current_clip = all_beep_clips.Count;    // high it if too low OR trying looped randoms
                    //beep_manager[sel_beep_src].source.clip = all_beep_clips[beep_manager[sel_beep_src].current_clip - 1];   //set beep clip
                }


                if (bm.current_clip == 0)
                {
                    //Random clip

                    //if precise is true, checked for looped random and disable if needed
                    //no. just show label
                    //if (beep_manager[sel_beep_src].precise_freq == 0) beep_manager[sel_beep_src].current_clip = 1;  //disable random for looped sounds
                    //else if (beep_manager[sel_beep_src].precise_freq != 0) GUILayout.Label("Random beep", label_txt_center, GUILayout.ExpandWidth(true));   //allow random beep if not looped
                    GUILayout.Label("Random", label_txt_center, GUILayout.ExpandWidth(true)); //allow random beep for loose timing
                }
                else GUILayout.Label("Beep " + bm.current_clip, label_txt_center, GUILayout.ExpandWidth(true));



                //no option for random clip when clicking -->
                if (GUILayout.Button("-->", GUILayout.ExpandWidth(true)))
                {
                    bm.current_clip++;


                    //first, if bm.current_clip > all_beeps.Count : current_clip = 0;
                    // don't set clip, gets done in Update() when random
                    if (bm.current_clip > all_beep_clips.Count) bm.current_clip = 0;
                    //second, if current_clip == 0 && precise && precise_freq == 0 : current_clip = 1
                    if (bm.current_clip == 0 && bm.precise && bm.precise_freq == 0)
                    {
                        bm.current_clip = 1;
                        bm.source.clip = all_beep_clips[bm.current_clip - 1];
                    }
                    else if (bm.current_clip > 0) bm.source.clip = all_beep_clips[bm.current_clip - 1];
                    //if (beep_manager[sel_beep_src].current_clip > all_beep_clips.Count || (beep_manager[sel_beep_src].current_clip == 0 && beep_manager[sel_beep_src].precise_freq == 0)) beep_manager[sel_beep_src].current_clip = 1;  // low it if too high OR trying looped randoms
                    //beep_manager[sel_beep_src].source.clip = all_beep_clips[beep_manager[sel_beep_src].current_clip - 1];   //set beep clip
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                //No beep clips in list
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("\n-- BEEPS DISABLED --\n\n-- NO SOUND FILES TO PLAY --", label_txt_red_center);
                GUILayout.EndHorizontal();
            }
        }

        private void sliders_gui()
        {
            if (vessel.GetCrewCount() > 0) chatter_sliders();   //show chatter sliders if crew > 0

            sstv_sliders();

            beep_sliders();
        }

        private void audiosets_gui()
        {
            int i;
            for (i = 0; i < chatter_array.Count; i++)
            {

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                bool temp = chatter_array[i].is_active;
                chatter_array[i].is_active = GUILayout.Toggle(chatter_array[i].is_active, chatter_array[i].directory + " (" + (chatter_array[i].capcom.Count + chatter_array[i].capsule.Count).ToString() + " clips)", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Remove", GUILayout.ExpandWidth(false)))
                {
                    chatter_array.RemoveAt(i);
                    string dirs = "";

                    int x;
                    for (x = 0; x < chatter_array.Count; x++)
                    {
                        dirs += chatter_array[x].directory + "," + chatter_array[x].is_active + "\n";
                    }

                    KSP.IO.File.WriteAllText<chatterer>(dirs, "audiosets.cfg", null);
                    load_all_chatter();
                    break;
                }

                if (temp != chatter_array[i].is_active) load_toggled_sets();    //reload toggled audio clips if any set is toggled on/off

                GUILayout.EndHorizontal();

            }

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            custom_dir_name = GUILayout.TextField(custom_dir_name, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Load", GUILayout.ExpandWidth(false)))
            {
                bool already_loaded = false;
                foreach (RBRAudioList r in chatter_array)
                {
                    if (custom_dir_name == r.directory) already_loaded = true;
                }

                if (custom_dir_name.Trim() != "" && custom_dir_name != "directory name" && already_loaded == false)
                {

                    //dump current audio_dirs to a list
                    string dirs = "";
                    int x;
                    for (x = 0; x < chatter_array.Count; x++)
                    {
                        dirs += chatter_array[x].directory + "," + chatter_array[x].is_active + "\n";
                    }
                    //add custom dir to list
                    dirs += custom_dir_name.Trim() + "," + true;
                    //rewrite list to file
                    KSP.IO.File.WriteAllText<chatterer>(dirs, "audiosets.cfg", null);
                    //reload audio
                    load_all_chatter();
                    //reset custom_dir_name
                    custom_dir_name = "directory name";
                }
            }

            GUILayout.EndHorizontal();
        }

        private void settings_gui()
        {
            if (vessel.GetCrewCount() > 0)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                http_update_check = GUILayout.Toggle(http_update_check, "Allow update check (http)");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                disable_beeps_during_chatter = GUILayout.Toggle(disable_beeps_during_chatter, "Disable beeps during chatter");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                remotetech_toggle = GUILayout.Toggle(remotetech_toggle, "Enable RemoteTech integration");
                GUILayout.EndHorizontal();

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

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                if (set_insta_chatter_key == false)
                {
                    GUILayout.Label("Insta-chatter key: " + insta_chatter_key.ToString(), label_txt_left, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Change", GUILayout.ExpandWidth(false))) set_insta_chatter_key = true;
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

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                if (set_insta_sstv_key == false)
                {
                    GUILayout.Label("Insta-SSTV key: " + insta_sstv_key.ToString(), label_txt_left, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Change", GUILayout.ExpandWidth(false))) set_insta_sstv_key = true;
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
            }

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            misc_show_skins = GUILayout.Toggle(misc_show_skins, "Skins", GUI.skin.button, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            if (misc_show_skins)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                if (GUILayout.Button("No skin", GUILayout.ExpandWidth(true))) gui_style = -9;
                if (GUILayout.Button("HighLogic", GUILayout.ExpandWidth(true))) gui_style = 0;
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                if (GUILayout.Button("KSP_1", GUILayout.ExpandWidth(true))) gui_style = 1;
                if (GUILayout.Button("KSP_2", GUILayout.ExpandWidth(true))) gui_style = 2;
                if (GUILayout.Button("KSP_3", GUILayout.ExpandWidth(true))) gui_style = 3;
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            if (changing_icon_pos == false)
            {
                if (GUILayout.Button("Change icon position")) changing_icon_pos = true;
            }
            else GUILayout.Label("Click anywhere to set new icon position");
            GUILayout.EndHorizontal();
        }

        private void main_gui(int window_id)
        {
            GUILayout.BeginVertical();

            if (power_available)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                if (GUILayout.Button("Sliders", gs_menu_sliders, GUILayout.ExpandWidth(true)))
                {
                    reset_menu_gs();
                    gs_menu_sliders = button_txt_center_green;
                    active_menu = "sliders";
                }

                if (vessel.GetCrewCount() > 0)
                {
                    // only show audiosets if crew is on board to chatter
                    if (GUILayout.Button("Sets", gs_menu_audiosets, GUILayout.ExpandWidth(true)))
                    {
                        reset_menu_gs();
                        gs_menu_audiosets = button_txt_center_green;
                        active_menu = "audiosets";
                    }
                }

                if (GUILayout.Button("Settings", gs_menu_settings, GUILayout.ExpandWidth(true)))
                {
                    reset_menu_gs();
                    gs_menu_settings = button_txt_center_green;
                    active_menu = "settings";
                }

                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
                GUILayout.Label(line_512x4, GUILayout.ExpandWidth(false), GUILayout.Width(275), GUILayout.Height(10f));
                GUILayout.EndHorizontal();

                //show currently selected gui section
                if (active_menu == "sliders") sliders_gui();
                else if (active_menu == "audiosets") audiosets_gui();
                else if (active_menu == "settings") settings_gui();

                //new version info (if any)
                if (recvd_latest_version && latest_version != "")
                {
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label(latest_version, label_txt_left);
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                // No ElectricCharge available
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("-- POWER LOST --", label_txt_red_center);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        protected void draw_GUI()
        {
            if (gui_style == 0) GUI.skin = HighLogic.Skin;
            else if (gui_style == 1) GUI.skin = AssetBase.GetGUISkin("KSP window 1");
            else if (gui_style == 2) GUI.skin = AssetBase.GetGUISkin("KSP window 2");
            else if (gui_style == 3) GUI.skin = AssetBase.GetGUISkin("KSP window 3");
            else if (gui_style == 4) GUI.skin = AssetBase.GetGUISkin("KSP window 4");
            else GUI.skin = null;

            if (gui_styles_set == false) set_gui_styles();

            if (ui_icons_loaded)
            {
                ui_icon = ui_icon_off;
                //if (chatter_freq > 0 || sstv_freq > 0 || beep_manager[0].precise_freq > -1 || beep_manager[1].precise_freq > -1 || beep_manager[2].precise_freq > -1 || beep_manager[0].loose_freq > 0 || beep_manager[1].loose_freq > 0 || beep_manager[2].loose_freq > 0) ui_icon = ui_icon_on;
                if (chatter_freq > 0 || sstv_freq > 0 || (beep_manager[0].precise && beep_manager[0].precise_freq > -1) || (beep_manager[1].precise && beep_manager[1].precise_freq > -1) || (beep_manager[2].precise && beep_manager[2].precise_freq > -1) || (beep_manager[0].precise == false && beep_manager[0].loose_freq > 0) || (beep_manager[1].precise == false && beep_manager[1].loose_freq > 0) || (beep_manager[2].precise == false && beep_manager[2].loose_freq > 0)) ui_icon = ui_icon_on;
                if (GUI.Button(new Rect(ui_icon_pos), ui_icon, new GUIStyle())) main_gui_minimized = !main_gui_minimized;
            }
            else
            {
                if (GUI.Button(new Rect(ui_icon_pos), "Chatterer", GUI.skin.button)) main_gui_minimized = !main_gui_minimized;
            }

            if (main_gui_minimized == false) window_0_pos = GUILayout.Window(-526925713, window_0_pos, main_gui, "Chatterer " + this_version, GUILayout.Width(275), GUILayout.Height(50));
        }

        public void Awake()
        {
            if (debugging) Debug.Log("[CHATR] Awake() starting...");
            int i;
            for (i = 0; i < 3; i++)
            {
                //Create a new entry for each beep source FIRST
                beep_manager.Add(new RBRBeepManager());
            }

            load_settings();    //run before setting audiosource volumes and beep clips
            load_icons();
            if (GameDatabase.Instance.ExistsTexture("RBR/Textures/line_512x4")) line_512x4 = GameDatabase.Instance.GetTexture("RBR/Textures/line_512x4", false);
            if (http_update_check) get_latest_version();

            //load audio

            load_beep_clips();
            foreach (RBRBeepManager bm in beep_manager)
            {
                //re-iterate through and setup
                bm.source = audioplayer.AddComponent<AudioSource>();
                bm.source.panLevel = 0;     //set as 2D audiosource
                bm.source.volume = bm.vol_slider;
                if (all_beep_clips.Count > 0)
                {
                    if (bm.current_clip == 0) bm.source.clip = all_beep_clips[rand.Next(0, all_beep_clips.Count)];  // set random
                    else bm.source.clip = all_beep_clips[bm.current_clip - 1];
                }
                if (bm.precise == false) new_beep_loose_timer_limit(bm);
            }


            sstv = audioplayer.AddComponent<AudioSource>();
            sstv.volume = sstv_vol_slider;
            sstv.panLevel = 0;  //set as 2D audiosource
            load_all_sstv_clips();
            new_sstv_loose_timer_limit();

            initial_chatter = audioplayer.AddComponent<AudioSource>();
            initial_chatter.volume = chatter_vol_slider;
            initial_chatter.panLevel = 0;   //set as 2D audiosource
            response_chatter = audioplayer.AddComponent<AudioSource>();
            response_chatter.volume = chatter_vol_slider;
            response_chatter.panLevel = 0;
            load_all_chatter();

            quindar1 = audioplayer.AddComponent<AudioSource>();
            quindar1.volume = quindar_vol_slider;
            quindar1.panLevel = 0;  //set as 2D audiosource
            quindar2 = audioplayer.AddComponent<AudioSource>();
            quindar2.volume = quindar_vol_slider;
            quindar2.panLevel = 0;  //set as 2D audiosource
            load_quindar();

            initialize_new_exchange();

            if (debugging) Debug.Log("[CHATR] Awake() has finished...");
        }

        public void Update()
        {
            //Insta-... key setup
            if (insta_chatter_key_just_changed && Input.GetKeyUp(insta_chatter_key)) insta_chatter_key_just_changed = false;
            if (insta_sstv_key_just_changed && Input.GetKeyUp(insta_sstv_key)) insta_sstv_key_just_changed = false;

            //Icon relocation
            if (changing_icon_pos && Input.GetMouseButtonDown(0))
            {
                ui_icon_pos = new Rect(Input.mousePosition.x - 15f, Screen.height - Input.mousePosition.y - 15f, 30f, 30f);
                changing_icon_pos = false;
            }

            if (FlightGlobals.ActiveVessel != null)
            {
                vessel = FlightGlobals.ActiveVessel;

                if (run_once)
                {
                    //get null refs trying to set these in Awake() so do them once here
                    prev_vessel = vessel;
                    vessel_prev_sit = vessel.situation;
                    vessel_prev_stage = vessel.currentStage;
                    vessel_part_count = vessel.parts.Count;
                    run_once = false;
                }

                if (vessel != prev_vessel)
                {
                    //active vessel has changed
                    if (debugging) Debug.Log("[CHATR] ActiveVessel has changed::prev = " + prev_vessel.vesselName + ", curr = " + vessel.vesselName);

                    stop_audio("all");


                    //play a new clip any time vessel changes and new vessel has crew or is EVA
                    //if (((power_available && vessel.GetCrewCount() > 0) || vessel.vesselType == VesselType.EVA) && chatter_freq > 0)
                    //{
                    //new active vessel has crew onboard or is EVA
                    //play an auto clip
                    //pod_begins_exchange = true;
                    //begin_exchange(0);
                    //}

                    vessel_prev_sit = vessel.situation;
                    vessel_prev_stage = vessel.currentStage;
                    //don't update vessel_part_count here!
                    prev_vessel = vessel;
                }

                if (gui_running == false) start_GUI();

                //write settings every x seconds
                cfg_update_timer += Time.deltaTime;
                if (cfg_update_timer >= 7f)
                {
                    write_settings();
                    cfg_update_timer = 0;
                }

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

                consume_resources();    //try to use a little ElectricCharge

                if (power_available)
                {
                    //do SSTV

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

                    //do beeps
                    if (all_beep_clips.Count > 0)
                    {
                        foreach (RBRBeepManager bm in beep_manager)
                        {
                            if (bm.precise)
                            {
                                //precise beeps
                                if (bm.precise_freq == -1)
                                {
                                    //beeps turned off at slider
                                    bm.source.Stop();
                                }
                                else if (bm.precise_freq == 0)
                                {
                                    //looped beeps

                                    //disable looped sounds during chatter
                                    if ((disable_beeps_during_chatter && exchange_playing) || sstv.isPlaying)
                                    {
                                        bm.source.Stop();
                                    }
                                    else
                                    {
                                        bm.source.loop = true;
                                        if (bm.source.isPlaying == false) bm.source.Play();
                                    }
                                }
                                else
                                {
                                    //timed beeps
                                    if (bm.source.loop)
                                    {
                                        //if looping stop playing and set loop to off
                                        bm.source.Stop();
                                        bm.source.loop = false;
                                    }
                                    //then check the time
                                    bm.timer += Time.deltaTime;
                                    if (bm.timer > bm.precise_freq)
                                    {
                                        bm.timer = 0;
                                        //randomize beep if set to random (0)
                                        if (bm.current_clip == 0) bm.source.clip = all_beep_clips[rand.Next(0, all_beep_clips.Count)];
                                        //play beep unless disable == true && exchange_playing == true
                                        if (sstv.isPlaying || (exchange_playing && disable_beeps_during_chatter)) return;   //no beep under these conditions
                                        //if (disable_beeps_during_chatter == false || (disable_beeps_during_chatter == true && exchange_playing == false))
                                        else bm.source.Play();  //else beep
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
                                    bm.source.Stop();
                                }
                                else
                                {
                                    bm.timer += Time.deltaTime;
                                    if (bm.timer > bm.loose_timer_limit)
                                    {
                                        bm.timer = 0;   //reset timer
                                        new_beep_loose_timer_limit(bm);    //set a new loose limit
                                        //randomize beep if set to random (0)
                                        if (bm.current_clip == 0) bm.source.clip = all_beep_clips[rand.Next(0, all_beep_clips.Count)];
                                        if (sstv.isPlaying || (exchange_playing && disable_beeps_during_chatter)) return;   //no beep under these conditions
                                        //if (disable_beeps_during_chatter == false || (disable_beeps_during_chatter == true && exchange_playing == false) || sstv.isPlaying == false)
                                        else bm.source.Play();  //else beep

                                    }
                                }
                            }
                        }
                    }

                    //do chatter
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

                        if (chatter_freq > 0)
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
                                    //part count or stage has changed
                                    if (debugging) Debug.Log("[CHATR] beginning exchange,parts/staging");
                                    pod_begins_exchange = true;
                                    begin_exchange(rand.Next(0, 3));  //delay Play for 0-2 seconds for randomness
                                }

                                if (vessel.situation != vessel_prev_sit && sstv.isPlaying == false)
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
                else
                {
                    //power not available
                    stop_audio("all");
                }
                vessel_prev_sit = vessel.situation;
                vessel_prev_stage = vessel.currentStage;
                vessel_part_count = vessel.parts.Count;
            }
            else
            {
                //FlightGlobals.ActiveVessel == null
                if (gui_running) stop_GUI();
            }
        }
    }
}