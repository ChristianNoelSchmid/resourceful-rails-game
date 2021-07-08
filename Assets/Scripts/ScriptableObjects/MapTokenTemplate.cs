using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rails.ScriptableObjects
{
    [CreateAssetMenu(fileName = "TokenTemplate", menuName = "ScriptableObjects/Map/MapTokenTemplate", order = 2)]
    public class MapTokenTemplate : ScriptableObject
    {
        [SerializeField] private GameObject _clear;
        public GameObject Clear => _clear;

        [SerializeField] private GameObject _mountain;
        public GameObject Mountain => _mountain;

        [SerializeField] private GameObject _smallCity;
        public GameObject SmallCity => _smallCity;

        [SerializeField] private GameObject _mediumCity;
        public GameObject MediumCity => _mediumCity;

        [SerializeField] private GameObject _majorCity;
        public GameObject MajorCity => _majorCity;
    }
}