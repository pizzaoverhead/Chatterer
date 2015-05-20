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
        //Chatter filters window
        private bool show_chatter_filter_settings = false;
        protected Rect chatter_filter_settings_window_pos = new Rect(Screen.width / 2, Screen.height / 2, 10f, 10f);
        private int chatter_filter_settings_window_id;

        //GUI
        private int chatter_sel_filter;     //currently selected filter in filters window

        //Chatter filters
        private AudioChorusFilter chatter_chorus_filter;
        private AudioDistortionFilter chatter_distortion_filter;
        private AudioEchoFilter chatter_echo_filter;
        private AudioHighPassFilter chatter_highpass_filter;
        private AudioLowPassFilter chatter_lowpass_filter;
        private AudioReverbFilter chatter_reverb_filter;
        private int chatter_reverb_preset_index = 0;

        //Clipboards
        private ConfigNode filters_clipboard;
        private ConfigNode chorus_clipboard;
        private ConfigNode dist_clipboard;
        private ConfigNode echo_clipboard;
        private ConfigNode hipass_clipboard;
        private ConfigNode lopass_clipboard;
        private ConfigNode reverb_clipboard;

        //Unsorted
        private ConfigNode filter_defaults;


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
    }
}
