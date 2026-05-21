using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;


//public class BuildingUI : MonoBehaviour
//{
//    public static BuildingUI Instance;

//    public GameObject panel;
//    public Button trainButton;
//    public TMP_Text infoText;

//    private EntityManager entityManager;

//    void Awake()
//    {
//        Instance = this;
//    }

//    void Start()
//    {
//        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

//        trainButton.onClick.AddListener(OnTrainClicked);

//        Refresh();
//    }

//    void Update()
//    {
//        Refresh();
//    }

//    void OnTrainClicked()
//    {
//        BuildingSelectionManager.Instance.TrainUnit();
//    }

//    public void Refresh()
//    {
//        Entity selected =
//            BuildingSelectionManager.Instance != null
//                ? BuildingSelectionManager.Instance.SelectedBuilding
//                : Entity.Null;

//        if (selected == Entity.Null || !entityManager.Exists(selected))
//        {
//            //panel.SetActive(false);
//            return;
//        }

//        bool hasProduction =
//            entityManager.HasComponent<ProductionData>(selected);

//        if (!hasProduction)
//        {
//            //panel.SetActive(false);
//            return;
//        }

//        panel.SetActive(true);

//        bool underConstruction =
//            entityManager.HasComponent<UnderConstructionTag>(selected);

//        ProductionData prod =
//            entityManager.GetComponentData<ProductionData>(selected);

//        trainButton.interactable =
//            !underConstruction &&
//            prod.QueueCount < prod.MaxQueue;

//        if (infoText != null)
//        {
//            if (underConstruction)
//            {
//                infoText.text = "Building...";
//            }
//            else
//            {
//                infoText.text =
//                    $"Queue: {prod.QueueCount}/{prod.MaxQueue}\n" +
//                    $"Time: {prod.TimeRemaining:0.0}s";
//            }
//        }
//    }
//}
