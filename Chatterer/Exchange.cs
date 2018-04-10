using System;
using System.Collections;
using System.Collections.Generic;
//using System.Linq;
//using System.Text;
using UnityEngine;

namespace Chatterer
{
    public partial class chatterer : MonoBehaviour
    {
        IEnumerator Exchange(float delay)
        {
            if (chatter_exists && (vessel.GetCrewCount() > 0))
            {
                if (debugging) Debug.Log("[CHATR] Beginning Cor_exchange");
                
                //begin_exchange;
                exchange_playing = true;
                initialize_new_exchange();

                if (initial_chatter_source == 1)
                {
                    //capsule starts the exchange
                    //always play regardless of radio contact state
                    if (debugging) Debug.Log("[CHATR] Capsule starts the exchange...");

                    //play initial capsule chatter
                    if (initial_chatter_set.Count > 0)
                    {
                        if (initial_chatter.isPlaying == false)
                        {
                            initial_chatter.Play();

                            yield return new WaitForSeconds(initial_chatter.clip.length);
                            //initial chatter has finished playing
                        }
                        else if (debugging) Debug.LogWarning("[CHATR] initial_chatter already playing, move on...");
                    }
                    else
                    {
                        exchange_playing = false;
                        if (debugging) Debug.LogWarning("[CHATR] initial_chatter_set has no audioclips, abandoning exchange");
                    }

                    ////add RT delay to response delay if enabled and in contact
                    //if (remotetech_toggle && inRadioContact) response_delay_secs += Convert.ToInt32(controlDelay);

                    ////if RT is enabled but not in radio contact, play no response
                    //if (remotetech_toggle && inRadioContact == false) exchange_playing = false;

                    //if there is no radio contact with KSC, play no response
                    //if (!inRadioContact) exchange_playing = false;

                }

                if (initial_chatter_source == 0)
                {
                    //capcom starts the exchange
                    if (debugging) Debug.Log("[CHATR] Capcom starts the exchange...");

                    if (inRadioContact)
                    {
                        //in radio contact,
                        //play initial capcom

                        if (initial_chatter_set.Count > 0)
                        {
                            //initial_chatter.PlayDelayed(delay);
                            if (quindar_toggle) play_quindar(0);    // play with quindar
                            else initial_chatter.PlayDelayed(0); // play without quindar
                        }
                        else
                        {
                            exchange_playing = false;
                            if (debugging) Debug.LogWarning("[CHATR] initial_chatter_set has no audioclips, abandoning exchange");
                        }
                    }
                    else
                    {
                        //not in radio contact,
                        //play no initial chatter or response
                        exchange_playing = false;
                    }
                }

                if (debugging) Debug.Log("[CHATR] Responding Cor_exchange, delay = " + response_delay_secs);
                yield return new WaitForSeconds(response_delay_secs);
                
                //so respond now
                if (vessel.GetCrewCount() > 0 && response_chatter_started == false)
                {
                    if (response_chatter_set.Count > 0 && (inRadioContact))
                    {
                        if (debugging) Debug.Log("[CHATR] playing response");
                        response_chatter_started = true;
                        if (initial_chatter_source == 1)
                        {
                            if (debugging) Debug.Log("[CHATR] Capcom responding");

                            if (quindar_toggle)
                            {
                                quindar1.PlayDelayed(delay);
                                //print("playing response first quindar");
                                response_chatter.PlayDelayed(delay + quindar1.clip.length);
                                //print("playing response chatter");
                                quindar2.PlayDelayed(delay + quindar1.clip.length + response_chatter.clip.length);
                                //print("playing response second quindar");
                            }
                            else response_chatter.PlayDelayed(delay);
                        }
                        if (initial_chatter_source == 0)
                        {
                            if (response_chatter.isPlaying == false)
                            {
                                if (debugging) Debug.Log("[CHATR] Capsule responding");

                                response_chatter.Play();

                                yield return new WaitForSeconds(response_chatter.clip.length);
                                //response chatter has finished playing
                            }
                            else if (debugging) Debug.LogWarning("[CHATR] response_chatter already playing, move on...");
                        }
                    }
                    else if (response_chatter_set.Count > 0 && !inRadioContact)
                    {
                        if (exchange_playing == true)
                        {
                            if (debugging) Debug.Log("[CHATR] No connection, no response ... you are alone !");
                            exchange_playing = false;
                        }
                    }
                    else
                    {
                        if (debugging) Debug.LogWarning("[CHATR] response_chatter_set has no audioclips, abandoning exchange");
                        exchange_playing = false;   //exchange is over
                    }
                }

                response_chatter_started = false;
                exchange_playing = false;
                if (debugging) Debug.Log("[CHATR] exchange is over");
            }
        }
    }
}
