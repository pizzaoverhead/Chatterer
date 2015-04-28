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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chatterer
{
    public partial class chatterer
    {
        private bool debugging = false;      //lots of extra log info if true

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
        private string menu = "chatter";    //default to chatter menu because it has to have something

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
        private KeyCode insta_chatter_key = KeyCode.None;
        private bool set_insta_chatter_key = false;
        private bool insta_chatter_key_just_changed = false;

        //Insta-SSTV key
        private KeyCode insta_sstv_key = KeyCode.None;
        private bool set_insta_sstv_key = false;
        private bool insta_sstv_key_just_changed = false;

        private bool mute_all = false;
        private bool all_muted = false;

        private bool show_advanced_options = false;
        private bool show_chatter_sets = false;

        //Clipboards
        private ConfigNode beepsource_clipboard;

        //Settings nodes
        private string settings_path;
        private ConfigNode plugin_settings_node;
        private ConfigNode vessel_settings_node;

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
    }
}
