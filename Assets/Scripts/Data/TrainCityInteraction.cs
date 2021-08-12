using Rails.Data;

namespace Assets.Scripts.Data
{
    public class TrainCityInteraction
    {
        public int PlayerIndex { get; set; }
        public City City { get; set; }
        public NodeId TrainPosition { get; set; }
    }
}
