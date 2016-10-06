using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(SphereCollider))] //total volume presence of the group
public class GroupController : MonoBehaviour
{
    private Vector3 lastPos, deltaMovement;
    private int gridedWeight;

    private GameObject bar;
    protected Transform squadObject;
    protected bool continueRunning = true;

    public float Speed
    {
        get
        {
            return GroupGridComponent.BaseSpeed;
        }
    }

    void Awake()
    {
        volumeCollider = GetComponent<SphereCollider>();

        GroupGridComponent = GetComponent<E_GridedMovement>();
        HashSet<GroupUnit> temp = new HashSet<GroupUnit>();
        squadObject = transform.Find("squad");
        foreach (GroupUnit g in squadObject.gameObject.GetComponentsInChildren<GroupUnit>())
        {
            temp.Add(g);
            g.Squad = this;
        }
        squad = temp;

        squadObject.name = name + ": " + squadObject.name;
        squadObject.parent = GameObject.Find("EntityHierarchy").transform;
    }

    void Start()
    {
        lastPos = transform.position;

        bar = Instantiate(new GameObject()) as GameObject;
        bar.name = "Center of Mass";
        bar.transform.position = this.transform.position;
        bar.transform.parent = this.transform;

        gridedWeight = squad.Count * 4;
        if (squad.Count <= 1)
        {
            SetVolumeRadius(1f);
            volumeCollider.radius = 1f;
        }
        else
        {
            currentVolumeRadius = volumeCollider.radius;
        }
        /////TODO change starting radius physical
        foreach (GroupUnit unit in squad)
        {
            unit.SquadSendInit();
        }
        StartCoroutine(UpdateCenterOfMass());
    }

    // Update is called once per frame
    void Update()
    {
        deltaMovement = transform.position - lastPos;

        foreach (GroupUnit unit in squad)
        {
            unit.MovementOrders += deltaMovement;
        }
        lastPos = transform.position;
    }

    protected int unitsLost = 0;
    public void UnitLost()
    {
        if (unitsLost == 0)
        {
            StartCoroutine(Regroup());
        }
        unitsLost++;     
    }

    public void UnitRecovered()
    {

    }

    protected IEnumerator Regroup()
    {
        GroupGridComponent.SpeedModifier = .5f;
        yield return new WaitForSeconds(.5f);
        while(unitsLost > 0)
        {
            GroupGridComponent.SpeedModifier -= .05f;
            yield return new WaitForSeconds(.5f);
        }
    }

    public Vector3 CenterOfMass { get; private set; }
    private IEnumerator UpdateCenterOfMass()
    {
        float momentX;
        float momentY;
        float momentZ;

        while (continueRunning)
        {
            momentX = transform.position.x * gridedWeight;
            momentY = transform.position.y * gridedWeight;
            momentZ = transform.position.z * gridedWeight;

            foreach (GroupUnit unit in squad)
            {
                momentX += unit.Pos.x;
                momentY += unit.Pos.y;
                momentZ += unit.Pos.z;
            }

            CenterOfMass = new Vector3(momentX / (squad.Count + gridedWeight), momentY / (squad.Count + gridedWeight), momentZ / (squad.Count + gridedWeight));
            bar.transform.position = CenterOfMass;
            yield return new WaitForSeconds(.11f);
        }
    }

    #region components

    public E_GridedMovement GroupGridComponent { get; private set; }

    public HashSet<GroupUnit> squad { get; private set; }
    #endregion

    /*
        The volume collider-trigger is used to detect obstacles near the group and respond accordingly.
        When near an obstacle, reduce the volume size to keep the group closer together.
    */
    #region WorldVolumeControl    
    protected SphereCollider volumeCollider;
    public SphereCollider VolumeCollider
    {
        get { return volumeCollider; }
    }
    protected float currentVolumeRadius;
    public float CurrentVolumeRadius
    {
        get { return currentVolumeRadius; }
    }

    private void SetVolumeRadius(float radiusIn)
    {
        currentVolumeRadius = radiusIn;
        foreach (GroupUnit g in squad)
        {
            g.DistanceThreshold = radiusIn;
        }
    }

    protected HashSet<GameObject> neighborColliders = new HashSet<GameObject>();
    protected bool obstacleNear = false;
    protected Collider blockingEntity;
    private int bitwise;
    void OnTriggerEnter(Collider other)
    {
        if (other.transform.IsChildOf(squadObject))
            return;
        //bitwise = (1 << other.gameObject.layer) & WorldManager.entityFlag;
        //if (bitwise != 0) 
        //{
        //    blockingEntity = other;   
        //    GroupGridComponent.PauseMoving();
        //}
        if (other.gameObject.layer == 13)
        {
            if (neighborColliders.Contains(other.gameObject))
                return;
            neighborColliders.Add(other.gameObject);
            if (volumeCollider.radius == currentVolumeRadius)
                SetVolumeRadius(currentVolumeRadius / 2f);
        }
    }

    void OnTriggerExit(Collider other)
    {
        //if(other == blockingEntity)
        //{
        //    GroupGridComponent.ResumeMoving();
        //    blockingEntity = null;
        //    return;
        //}
        if (neighborColliders.Contains(other.gameObject))
        {
            neighborColliders.Remove(other.gameObject);
            if (neighborColliders.Count <= 0)
            {
                SetVolumeRadius(volumeCollider.radius);
            }
        }
    }

    /*
     * Call upon another movable entity entering the trigger area.
     * Send a message to the other entity, if the other entity is closer to its goal block self
     * otherwise, block the other entity
     * */
    public void TryToBlockMovement(GroupController otherGroup)
    {
        throw new System.NotImplementedException();
    }
    #endregion
}
