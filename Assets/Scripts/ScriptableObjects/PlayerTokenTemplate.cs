using UnityEngine;

namespace Rails.ScriptableObjects
{
    [CreateAssetMenu(fileName = "PlayerTemplate", menuName = "ScriptableObjects/Players/PlayerTokenTemplate", order = 2)]
    public class PlayerTokenTemplate : ScriptableObject
    {
        [SerializeField]
        private string _trainName;
        public string TrainName => _trainName;

        [SerializeField]
        private GameObject _trainToken;
        public GameObject TrainToken => _trainToken;

        [SerializeField]
        private GameObject _railToken;
        public GameObject RailToken => _railToken;
    }
}