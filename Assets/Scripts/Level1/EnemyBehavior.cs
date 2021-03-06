// -------------------------- EnemyBehavior.cs --------------------------------
// Author - Robert Griswold CSS 385
// Created - Apr 19, 2016
// Modified - April 28, 2016
// ----------------------------------------------------------------------------
// Purpose - Implementation for an enemy that has three states: Normal, Run, 
// and Stunned. See notes for details. Movement is only allowed via a variable 
// in the GameManager.
// ----------------------------------------------------------------------------
// Notes - Normal: Default state that can move forward and bounces off walls.
// Run: When 30 units in LOS of player (based on facing if available), changes 
// sprite animation and runs away.
// Stunned: Hit by a projectile and decreases lives. If killed, increments 
// core in GameManager.
// ----------------------------------------------------------------------------

using UnityEngine;
using System.Collections;

public class EnemyBehavior : MonoBehaviour
{
    #region State variables
    public enum EnemyState
    {
        Normal = 0,
        Run = 1,
        Stunned =2
    }
    public EnemyState currentState = EnemyState.Normal;
    public int Lives = 3; //number of lives the entity has
    [SerializeField] private float checkFacingAngle = 0.8f;
    [SerializeField] private AudioClip deathSound = null;
    
    private float timeLeft = 0.0f; //timer for stunned state
    private int deathDelay = 0; //indicates enetity died and is playing out the death sound
    #endregion

    #region Enemy movement variables
    public const int DELAY = 30; //number of frames to delay running redirection when near a boundary
    public float Speed = 30.0f;
    public float TurnSpeed = 9.0f;
    [SerializeField] private float minSpeed = 20.0f;
    [SerializeField] private float maxSpeed = 40.0f;
    [SerializeField] private float towardsCenter = 0.4f;
        // what is the change of enemy flying towards the world center after colliding with world bound
        // 0: no control
        // 1: always towards the world center, no randomness

    private int boundaryBumpDelay = 0; //delay if hit a wall
    private WorldBound sceneBoundary = null;
    private LevelBehavior levelManager = null;
    private GameObject player = null;
    private PlayerControl playerController = null;
    private Vector3 directionV;
    #endregion

    #region Powerup Store
    public GameObject itemDrop = null;
    public float itemDropRate = .25f;
    #endregion

    // Use this for initialization
    void Start ()
    {
        #region References
        levelManager = GameObject.Find("GameManager").GetComponent<LevelBehavior>();
        if (levelManager == null)
        {
            Debug.LogError("GameManager not found for " + this + ".");
            Application.Quit();
        }

        sceneBoundary = GameObject.Find("GameManager").GetComponent<WorldBound>();
        if (sceneBoundary == null)
        {
            Debug.LogError("WorldBound not found for " + levelManager + " in " + this + ".");
            Application.Quit();
        }

        player = GameObject.Find("Hero");
        if (player == null)
        {
            Debug.LogError("Hero not found for " + this + ".");
        }
        else
        {
            playerController = player.GetComponent<PlayerControl>();
            if (playerController == null)
                Debug.LogError("PlayerControl not found for " + player + ".");
        }

        itemDrop = Resources.Load("Prefabs/PowerUp") as GameObject;
        if (itemDrop == null)
            Debug.LogError("PowerUp not found for " + this + ".");
        #endregion

        newDirection();
        newSpeed();
	}

	// Update is called once per frame
	void Update ()
    {
        //count down death timer and destroy if it reaches zero
        if (deathDelay != 0)
        {
            deathDelay--;
            if (deathDelay == 0)
                Destroy(gameObject);
        }

        #region Movement based on state
        //update timer and change state
        updateTimer();
        changeState();

        if (currentState == EnemyState.Run) //running
        {
            //turn away if not delayed by a boundary collision
            //if (boundaryBumpDelay-- <= 0 && transform.position.x > globalBehavior.WorldMin.x + buffer &&
            //    transform.position.x < globalBehavior.WorldMax.x - buffer &&
            //    transform.position.y > globalBehavior.WorldMin.y + buffer &&
            //    transform.position.y < globalBehavior.WorldMax.y - buffer)
            //{
            if (boundaryBumpDelay <= 0 && player != null)
            {
                //transform.up = (transform.position - player.transform.position);
                directionV = (transform.position - player.transform.position).normalized; //debug
            }

            //transform.position += (Speed * Time.smoothDeltaTime) * transform.up;
            transform.position += (Speed * Time.smoothDeltaTime) * directionV;
        }
        else if(currentState == EnemyState.Stunned) //stunned still
        {
            transform.Rotate(transform.forward, Time.deltaTime * TurnSpeed);
        }  
        else if (levelManager.Movement) //normal
        {
            //transform.position += (Speed * Time.smoothDeltaTime) * transform.up;
            transform.position += (Speed * Time.smoothDeltaTime) * directionV;
        }
        #endregion

        #region Bump off walls and clamp to world
        //count down bump off wall delay
        if (boundaryBumpDelay != 0)
        {
            boundaryBumpDelay--;
        }

        //check boundary collision
        WorldBound.WorldBoundStatus status =
            sceneBoundary.ObjectCollideWorldBound(GetComponent<Renderer>().bounds);
        if (status != WorldBound.WorldBoundStatus.Inside)
        {
            //Debug.Log("collided position: " + this.transform.position);
            newDirection();
            boundaryBumpDelay = DELAY;
        }

        //clamp to world
        sceneBoundary.ClampToWorld(transform, 1.0f);
        #endregion

    }

    #region State functions
    //creates a timer
    void createTimer(float time)
    {
        timeLeft = time;
    }

    //decrements time left
    bool updateTimer()
    {
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0)
            return true;

        return false;
    }

    //collision
    void OnTriggerEnter2D(Collider2D other)
    {
        //Debug.Log(other);
        //ignore anything other than Projectiles
        if (other.gameObject.name != "Projectile(Clone)")
            return;

        //change to stunned
        currentState = EnemyState.Stunned;
        createTimer(5.0f);

        //destroy projectile
        Destroy(other.gameObject);

        //decrease lives and destroy if out of lives
        if (--Lives <= 0)
        {
            levelManager.Score++;
            GetComponent<Renderer>().enabled = false; //hide it to allow the sound to play out
            Destroy(GetComponent<Rigidbody2D>()); //remove collision
            deathDelay = DELAY;
            
            if (Random.value < itemDropRate)
            {
                GameObject e = Instantiate(itemDrop) as GameObject;
                e.transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z);
            }

            //play death sound
            if (deathSound != null)
                GetComponent<AudioSource>().PlayOneShot(deathSound, 0.7f);

            
        }
        else
        {
            //play a hit sound
            GetComponent<AudioSource>().Play();
        }
    }

    //determines state between Normal and Run
    //stunned is handled by trigger event
    void changeState()
    {
        //determine distance and facing of player
        if (player == null)
            return;
        float distance = Vector3.Distance(player.transform.position, transform.position);
        float angle = 0.0f;

        //adjust angle for facing
        if (playerController != null)
        {
            switch (playerController.GetFacing())
            {
                case PlayerControl.Facing.East:
                    angle = Vector3.Dot(player.transform.right,
                        (transform.position - player.transform.position).normalized);
                    break;
                case PlayerControl.Facing.West:
                    angle = Vector3.Dot(-player.transform.right,
                        (transform.position - player.transform.position).normalized);
                    break;
                case PlayerControl.Facing.South:
                    angle = Vector3.Dot(-player.transform.up,
                        (transform.position - player.transform.position).normalized);
                    break;
                default:
                    angle = Vector3.Dot(player.transform.up,
                        (transform.position - player.transform.position).normalized);
                    break;
            }
        }

        //determine new state
        if (distance <= 30 && angle > checkFacingAngle && currentState != EnemyState.Stunned)
        {
            currentState = EnemyState.Run;
        }
        else if ((currentState == EnemyState.Run && distance > 30) 
            || (currentState == EnemyState.Stunned && timeLeft <= 0))
        {
            currentState = EnemyState.Normal;
            newSpeed();
            transform.rotation = new Quaternion(0, 0, 0, 0); //rotate back up
        }  
    }
    #endregion

    #region Movement functions
    // New direction will be something randomly within +- 45-degrees away from the direction
    // towards the center of the world
    //
    // To find an angle within +-45 degree of a direction: 
    //     1. consider the simplist case of 45-degree above or below the x-direction
    //	   2. we compute random.X: a randomly generate x-value between +1 and -1
    //     3. To ensure within 45 degrees, we simply need to make sure generating a y-value that is within the (-random.X to +random.X) range
    //     4. Now a direction towards the (random.X, random.Y) is guaranteed to be within 45-degrees from x-direction
    // Apply the above logic, only now:
    //		X-direciton is V (from current direciton towards the world center)
    //		Y-direciton is (V.y, -V.x)
    //
    // Lastly, 45-degree is nice because X=Y, we can do this for any angle that is less than 90-degree
    private void newDirection() {
		// we want to move towards the center of the world
		Vector2 v = sceneBoundary.WorldCenter - new Vector2(transform.position.x, transform.position.y);  
				// this is vector that will take us back to world center
		v.Normalize();
		Vector2 vn = new Vector2(v.y, -v.x); // this is a direciotn that is perpendicular to V

		float useV = 1.0f - Mathf.Clamp(towardsCenter, 0.01f, 1.0f);
		float tanSpread = Mathf.Tan(useV * Mathf.PI / 2.0f );

		float randomX = Random.Range(0f, 1f);
		float yRange = tanSpread * randomX;
		float randomY = Random.Range (-yRange, yRange);

		Vector2 newDir = randomX * v + randomY * vn;
		newDir.Normalize();
        //transform.up = newDir;
        directionV = newDir;
	}

    //changes speed to a random between minSpeed and maxSpeed
    private void newSpeed()
    {
        Speed = Random.Range(minSpeed, maxSpeed);
    }
    #endregion
}
