using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GroupUnit : MonoBehaviour, I_Entity
{
    public bool Killable
    {
        get; protected set;
    }

    public bool Dead
    {
        get; protected set;
    }

    public float BaseIntegrity
    {
        get; protected set;
    }

    public float Integrity
    {
        get; protected set;
    }

    //speed in meters per second - base speed, speedModification
    protected float baseSpeed, modSpeed;
    protected float groundYPos;
    protected Vector3 lastPos;
    protected float distanceFromCenter;
    private float distanceThreshold;
    public float DistanceThreshold
    {
        get
        {
            return distanceThreshold;
        }
        set
        {
            distanceThreshold = value / 2f;
            //collisionAvoidanceCollider.radius = distanceThreshold;
        }
    }

    //variable for storing temporary node
    protected RaycastHit[] hits; //variable for storing temporary RaycastHit array
    protected RaycastHit hit;

    protected CapsuleCollider characterCollider;
    protected Vector3 movingDirection; //direction character is moving

    protected bool findingPath = false; //flag indicates whether this character is already looking for a path or waiting in the queue
    protected bool moving = false;
    protected bool targetReached = false;

    protected bool canMove = true;
    public Vector3 Align { get; private set; }
    public Vector3 Cohesion { get; private set; }
    public Vector3 Separation { get; private set; }
    public Vector3 Avoid { get; private set; }
    protected float avoidFactor;

    void Awake()
    {
        characterCollider = GetComponent<CapsuleCollider>();
        collisionAvoidanceCollider = GetComponent<SphereCollider>();       
    }

    public void SquadSendInit()
    {
        DistanceThreshold = Squad.CurrentVolumeRadius;
        avoidFactor = 1f;
    }

    void FixedUpdate()
    {
        if(canMove)
            MovementPhase();
        lastPos = transform.position;
    }

    protected virtual void MovementPhase()
    {
        distanceFromCenter = Vector3.Distance(Pos, Squad.CenterOfMass);
        Align = Vector3.zero;
        Cohesion = Vector3.zero;
        Separation = Vector3.zero;
        Avoid = Vector3.zero;

        if (MovementOrders != Vector3.zero)
        {
            Align = ((MovementOrders + transform.position) - transform.position).normalized;
            MovementOrders = Vector3.zero;
            Debug.DrawRay(Pos, Align.normalized * 5f, Color.yellow);
            if (distanceFromCenter > DistanceThreshold)
            {
                Cohesion = (Squad.CenterOfMass - transform.position).normalized * 1.1f;
            }
        }
        else if (distanceFromCenter > DistanceThreshold * 2)
        {
            Cohesion = (Squad.CenterOfMass - transform.position).normalized;
            Debug.DrawRay(transform.position, Vector3.up * 6f, Color.cyan);
        }
        Debug.DrawRay(Pos, (Cohesion).normalized * 5f, Color.blue);

        float distance;
        int n_count = 0;
        //are there any detected units within the distance separation-trigger area
        if (neighborUnits.Count > 0)
        {
            foreach (GroupUnit unit in neighborUnits)
            {
                if (n_count > 5)
                    break;
                distance = Vector3.Distance(unit.Pos, transform.position);
                if (distance < distanceThreshold) //replace this with squad based spread
                    Separation += (unit.Pos - transform.position);
                n_count++;
            }
            Debug.DrawRay(Pos, Separation.normalized * 5f, Color.red);
        }
        //are there any detected obstacles within the distance separation-trigger area

        n_count = 0;
        if (neighborColliders.Count > 0)
        {
            foreach (GameObject neighborObject in neighborColliders)
            {
                if (n_count > 5)
                    break;
                distance = Vector3.Distance(neighborObject.transform.position, transform.position);
                if (distance < distanceThreshold)
                    Separation += (neighborObject.transform.position - transform.position) * 1 / distance;
                n_count++;
            }
        }
        if (neighborUnits.Count > 0 || neighborColliders.Count > 0)
        {
            Separation /= (neighborUnits.Count + neighborColliders.Count);
            Separation *= -1f;
            Separation = Separation.normalized;
            Debug.DrawRay(Pos, Separation.normalized * 5f, Color.red);
        }
        RaycastHit hit;

        //look for obstacles directly in front of the unit, try to avoid
        if (Physics.Raycast(transform.position, transform.forward, out hit, distanceThreshold / 2f, WorldGrid.mapFlag, QueryTriggerInteraction.Ignore))
        {
            Avoid = (transform.position + (transform.forward * distanceThreshold / 2f) - hit.collider.bounds.center);
            Avoid = Avoid.normalized;
            Debug.DrawRay(Pos, Avoid.normalized * distanceThreshold / 2f, Color.green);

        }
        Debug.DrawRay(Pos, transform.forward * 5.5f, Color.magenta);

        Debug.DrawRay(Pos, (Squad.CenterOfMass - transform.position) * 5.5f, (Color.black));
        if (Physics.Raycast(transform.position, (Squad.CenterOfMass - transform.position), Vector3.Distance(Squad.CenterOfMass, transform.position), WorldGrid.mapFlag, QueryTriggerInteraction.Ignore))
            Cohesion *= .25f;

        if (moving)
            MovementOrders += Avoid;
        //MovementOrders += Align;
        MovementOrders += Cohesion * (Mathf.Clamp(distanceFromCenter + DistanceThreshold, 0, 20f) / 20f) * .1f;
        MovementOrders += Separation * .3f;

        SetFacingDirection();
        if (MovementOrders != Vector3.zero)
        {
            transform.position += transform.forward.normalized * (8.9f + 0) * Time.deltaTime;// (1.5f * (Mathf.Clamp(distanceFromCenter, 0, 20f) / 20f))) * Time.deltaTime;
            moving = true;
        }
        else
        {
            moving = false;
        }

        hits = Physics.SphereCastAll(transform.position + (Vector3.up * characterCollider.bounds.size.y), characterCollider.bounds.extents.y * .1f, Vector3.down, 200f, (1 << 8));

        if (hits.Length > 0)
        {
            hit = hits[0];
            for (int x = 1; x < hits.Length; x++)
            {
                if (hit.point.y < hits[x].point.y)
                    hit = hits[x];
            }
            //Pos = new Vector3(transform.position.x, hit.point.y, transform.position.z);
            groundYPos = hit.point.y;
            //print(groundPosY);
        }
        else
            Debug.Log("Where is the ground?");
        Pos = new Vector3(transform.position.x, groundYPos, transform.position.z);
        //print(groundPosY);

        MovementOrders = Vector3.zero;
    }

    public void Startmoving()
    {
        canMove = true;
    }

    public void StopMoving()
    {
        canMove = false;
    }   

    public void SetFacingDirection()
    {
        try
        {
            if (MovementOrders == Vector3.zero)
                return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(new Vector3(MovementOrders.x, 0, MovementOrders.z)), Time.deltaTime * 5f);
        }
        catch (System.NullReferenceException e)
        {

        }
    }

    #region positionUtils
    public bool isCharacterAtPoint(Vector3 posIn)
    {
        try
        {
            if ((posIn - Pos).magnitude <= Mathf.Clamp(Time.deltaTime * Speed, characterCollider.bounds.extents.x, Mathf.Infinity))
                return true;
            return false;
        }
        catch (MissingReferenceException e)
        {
            return false;
        }
    }

    //find the given position, relative to the character bounds
    protected Vector3 ColliderOffsetPosition(Vector3 posIn)
    {
        return new Vector3(posIn.x,// + this.characterCollider.bounds.extents.x / 2f,
                            posIn.y + characterCollider.bounds.extents.y,
                            posIn.z);// + this.characterCollider.bounds.extents.z / 2f);
    }

    public float groundPosY
    {
        get { return groundPosY; }
    }

    public float BaseSpeed
    {
        get { return baseSpeed; }
        private set
        {
            baseSpeed = value;
        }
    }

    public float SpeedModifier
    {
        get { return modSpeed; }
        set
        {
            modSpeed = value;
        }
    }

    public float Speed
    {
        get { return baseSpeed * modSpeed; }
    }

    public float TurnSpeed { get; protected set; }

    /*
	 * Used to decide how many nodes are occupied by this character when pathing.
	 * */
    public float Clearance
    {
        get { return Squad.VolumeCollider.radius;  }
    }
    #endregion

    #region movementProperties
    public Vector3 MovementOrders { get; set; }

    /*
    * Use this to set or get the character's position
    * Get - Gets the current position
    * Set - Sets the position, offseting the value so that the collider is resting on the ground.
    * */

    public Vector3 Pos
    {
        get
        {
            try
            {
                return transform.position;
            }
            catch (MissingReferenceException e)
            {
                return Vector3.zero;
            }
        }
        protected set
        {
            transform.position = value + new Vector3(0, 0, 0);
        }
    }
    #endregion

    public void IncurDamage(float damageIn)
    {

    }

    public void DeathAction()
    {
        throw new System.NotImplementedException();
    }

    #region components
    public GameObject _AttachedGameObject
    {
        get
        {
            return gameObject;
        }
    }

    public GroupController Squad { get; set; }
    #endregion

    protected SphereCollider collisionAvoidanceCollider;
    protected List<GroupUnit> neighborUnits = new List<GroupUnit>();
    protected List<GameObject> neighborColliders = new List<GameObject>();
    void OnTriggerEnter(Collider other)
    {
        var unitComponent = other.GetComponent<GroupUnit>();

        if (unitComponent != null)
        {
            if (unitComponent != this && Squad.squad.Contains(unitComponent))
            {
                neighborUnits.Add(unitComponent);
            }
        }
        else if (other.gameObject.layer == 13)
        {
            neighborColliders.Add(other.gameObject);
        }
    }

    void OnTriggerExit(Collider other)
    {
        var unitComponent = other.GetComponent<GroupUnit>();

        if (unitComponent != null && unitComponent != this)
            neighborUnits.Remove(unitComponent);
        else if (other.gameObject.layer == 13)
        {
            neighborColliders.Remove(other.gameObject);
        }
    }
}
