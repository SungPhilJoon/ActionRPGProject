using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InfinityPBR.Demo
{
    public class MinotaurDemo : InfinityDemoCharacter
    {
        [Header("Texture Buttons")]
        public Button[] bodyButtons;
        public Button[] armorButtons;
        public Button[] axeButtons;
        public Button[] furButtons;

        private PrefabAndObjectManager _prefabAndObjectManager;

        public void Awake()
        {
            _prefabAndObjectManager = GetComponent<PrefabAndObjectManager>();
        }
        
        public void RandomizeTextures(){
            bodyButtons[Random.Range(0, bodyButtons.Length)].onClick.Invoke();
            armorButtons[Random.Range(0, armorButtons.Length)].onClick.Invoke();
            axeButtons[Random.Range(0, axeButtons.Length)].onClick.Invoke();
            furButtons[Random.Range(0, furButtons.Length)].onClick.Invoke();
            RandomizeWardrobe();
        }

        public void RandomizeWardrobe() =>  _prefabAndObjectManager.ActivateRandomAllGroups();
    }
}