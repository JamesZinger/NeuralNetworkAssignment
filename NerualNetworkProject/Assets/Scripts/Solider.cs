using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using ArtificialNeuralNetwork;

using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

[RequireComponent( typeof( SpriteRenderer ) )]
[RequireComponent( typeof( Rigidbody2D ) )]

public class Solider : MonoBehaviour
{

	#region Enums & Consts

	/// <summary> Values that represent Team.</summary>
	public enum Team : byte
	{
		Red,
		Blue
	}

	/// <summary> Values that represent for all possible effects on the solider.</summary>
	public enum EffectType : byte
	{
		Damage,
		Health,
		Speed
	}

	public enum AIState : byte
	{
		None,
		Flee,
		Fight,
		HealFriend,
		Patrol,
		Find
	}

	/// <summary> The solider maximum health.</summary>
	public const float SOLIDER_MAX_HEALTH = 100;

	/// <summary> The solider maximum ammo.</summary>
	public const int SOLIDER_MAX_AMMO = 1000;

	public const int SOLIDER_TRACK_FRIENDLY_COUNT = 4;

	public const int SOLIDER_TRACK_ENEMY_COUNT = 5;

	#endregion

	/// <summary> The movement speed.</summary>
	public float MovementSpeed = 5.0f;

	/// <summary> The damage each shot does.</summary>
	public float Damage = 10.0f;

	/// <summary> The range that the solider can fire</summary>
	public float Range = 2.0f;

	/// <summary> The amount of shots per second.</summary>
	public float ShotPerSecond = 10.0f;

	public Team AssignedTeam;

	public Sprite AliveSprite;
	public Sprite DeadSprite;

	[HideInInspector]
	public LevelManager Manager;

	public AIState aiState;

	#region Private Memebers

	private MLP neuralNetwork;

	private float health = SOLIDER_MAX_HEALTH;

	private int ammo = SOLIDER_MAX_AMMO;

	private float EffectedDamage;
	private float EffectedMovementSpeed;

	private bool isMoving;

	private Vector2 moveTarget;

	private Solider[] nearbyFriendlies;
	private Solider[] nearbyEnemies;

	private Solider attackTarget;

	private bool canShoot;

	#endregion

	#region Properties

	/// <summary> (read only) Gets or sets the health.</summary>
	public float Health
	{
		get { return health; }
		private set { health = value; }
	}

	/// <summary> (read only) Gets or sets the ammo.</summary>
	public int Ammo
	{
		get { return ammo; }
		private set { ammo = value; }
	}

	/// <summary> (read only) Gets the damage per second.</summary>
	public float DPS
	{
		get { return ( Damage * ShotPerSecond ); }
	}

	/// <summary> (read only) Gets the time between shots.</summary>
	public float TimeBetweenShots
	{
		get { return ( 1 / ShotPerSecond ); }
	}

	#endregion

	#region Unity Events

	void Awake()
	{
	}

	void Start()
	{
		Manager = GameObject.FindGameObjectWithTag( "Level" ).GetComponent<LevelManager>();

		if ( Health > SOLIDER_MAX_HEALTH )
		{
			Health = SOLIDER_MAX_HEALTH;
			UnityEngine.Debug.LogWarning( "Solider health is greater than the levels max health" );
		}

		if ( Ammo > SOLIDER_MAX_AMMO )
		{
			Ammo = SOLIDER_MAX_AMMO;
			UnityEngine.Debug.LogWarning( "Solider ammo is greater than the levels max ammo" );
		}

		switch ( AssignedTeam )
		{
			case ( Team.Blue ):

				gameObject.layer = LayerMask.NameToLayer( "BlueTeam" );

				break;
			case ( Team.Red ):

				gameObject.layer = LayerMask.NameToLayer( "RedTeam" );

				break;

			default:
				UnityEngine.Debug.LogException( new ArgumentException( "Solider cannot have a null team" ) );

				break;
		}

		GetComponent<SpriteRenderer>().sprite = AliveSprite;

		EffectedDamage = 0;
		EffectedMovementSpeed = 0;

		Manager.RegisterSolider( this );

		moveTarget = new Vector2( 0, 0 );
		isMoving = false;

		if ( AssignedTeam == Team.Red )
			gameObject.renderer.material.color = Color.red;

		else if ( AssignedTeam == Team.Blue )
			gameObject.renderer.material.color = Color.blue;

		nearbyEnemies = new Solider[ SOLIDER_TRACK_ENEMY_COUNT ];
		nearbyFriendlies = new Solider[ SOLIDER_TRACK_FRIENDLY_COUNT ];

		canShoot = true;

		///// Start up and train the NN /////
		
		// Set up the MLP and configure its inputs to receive inputs within the expected ranges
		// Note: This is to allow the MLP to normalize inputs to the range [0, 1]
		neuralNetwork = new MLP( 20, 20, 6, ActivationFunction.Threshold );
		for ( int i = 0; i < 5; i++ )
		{
			neuralNetwork.Inputs[ i ].MinValue =    0f; // Current health, friendly health
			neuralNetwork.Inputs[ i ].MaxValue =  100f;
		}
		for ( int i = 5; i < 10; i++ )
		{
			neuralNetwork.Inputs[ i ].MinValue =    0f; // Current ammo, friendly ammo
			neuralNetwork.Inputs[ i ].MaxValue = 1000f;
		}
		for ( int i = 10; i < 15; i++ )
		{
			neuralNetwork.Inputs[ i ].MinValue =    1f; // Nearest powerup distance, friendly distances
			neuralNetwork.Inputs[ i ].MaxValue =    5f;
		}
		for ( int i = 15; i < 20; i++ )
		{
			neuralNetwork.Inputs[ i ].MinValue =    1f; // Nearest powerup distance, friendly distances
			neuralNetwork.Inputs[ i ].MaxValue =    5f;
		}

		// Train the system with hardcoded training data
		TrainNN();
	}

	void Update()
	{
		if ( Input.GetMouseButtonDown( 0 ) && AssignedTeam == Team.Red )
		{
			Vector3 pos = Camera.main.ScreenToWorldPoint( Input.mousePosition );

			moveTarget = new Vector2( pos.x, pos.y );
			isMoving = true;
		}

		if ( isMoving )
		{
			if ( Manager.Debugging )
			{
				Debug.DrawLine( transform.position, moveTarget, Color.green );
			}
		}

		////////////////////////////////////////////
		/// AI Update

		calculateNearestEnemies();
		calculateNearestFriendlies();

		SelectState();

		switch ( aiState )
		{
			case ( AIState.Flee ):
				RunAway();
				break;
			case ( AIState.Fight ):
				Fight();
				break;
			case ( AIState.Find ):
				Debug.LogException( new NotImplementedException() );
				break;
			case ( AIState.HealFriend ):
				HealFriendly();
				Debug.LogException( new NotImplementedException() );
				break;
			case ( AIState.Patrol ):
				Patrol();
				Debug.LogException( new NotImplementedException() );
				break;
			case ( AIState.None ):
				break;
			default:
				Debug.LogException( new ArgumentException() );
				break;
		}
	}

	void FixedUpdate()
	{

		// Move Toward Target
		if ( isMoving )
		{

			Vector2 Direction = moveTarget - new Vector2( transform.position.x, transform.position.y );

			float theta = Mathf.Atan2( Direction.y, Direction.x );

			Direction.Normalize();

			theta = Mathf.Rad2Deg * theta;

			transform.rotation = Quaternion.AngleAxis( theta, new Vector3( 0, 0, 1 ) );

			rigidbody2D.velocity = ( Direction * MovementSpeed );

			Vector2 Diff = moveTarget - new Vector2( transform.position.x, transform.position.y );

			Diff.x = Mathf.Abs( Diff.x );
			Diff.y = Mathf.Abs( Diff.y );

			if ( Diff.x < 0.2f && Diff.y < 0.2f )
			{
				isMoving = false;
			}
		}
		else
		{
			rigidbody2D.velocity = Vector2.zero;
		}

		// Cap Velocity
		if ( rigidbody2D.velocity.magnitude > MovementSpeed + EffectedMovementSpeed )
		{
			Vector2 velocity = rigidbody2D.velocity;
			velocity.Normalize();
			velocity *= MovementSpeed;
			rigidbody2D.velocity = velocity;
		}


		// Cap Position
		Vector2 Position = transform.position;

		if ( !Manager.PlayableArea.Contains( Position ) )
		{

			Rect Area = Manager.PlayableArea;
			//Check the X coords

			if ( Position.x > Area.xMax )
				Position.x = Area.xMax - 0.05f;

			else if ( Position.x < Area.xMin )
				Position.x = Area.xMin + 0.05f;

			if ( Position.y > Area.yMax )
				Position.y = Area.yMax - 0.05f;

			else if ( Position.y < Area.yMin )
				Position.y = Area.yMin + 0.05f;

			transform.position = Position;

			isMoving = false;
			aiState = AIState.None;
		}

	}

	#endregion

	/// <summary> Executes the death action.</summary>
	void OnDeath()
	{

		GetComponent<SpriteRenderer>().sprite = DeadSprite;
		Manager.DeregisterSolider( this );
		collider2D.enabled = false;
	}

	/// <summary> Move to the specificed position.</summary>
	/// <param name="Position"> The position to move toward.</param>
	private void MoveTo( Vector2 Position )
	{
		moveTarget = Position;
		isMoving = true;
	}

	/// <summary> Applies the damage described by AppliedDamage.</summary>
	/// <param name="AppliedDamage"> The applied damage.</param>
	/// <returns> true if the Solider Died, false if it lived.</returns>
	public bool ApplyDamage( float AppliedDamage )
	{

		if ( AppliedDamage < 0 )
		{
			UnityEngine.Debug.LogException( new ArgumentOutOfRangeException( "Tried to apply Negative Damage!" ) );
		}

		Health -= AppliedDamage;

		if ( Health <= 0 )
		{
			OnDeath();
			return true;
		}
		return false;
	}

	/// <summary> Applies the healing described by HealingAmount.</summary>
	/// <param name="HealingAmount"> The healing amount.</param>
	public void ApplyHealing( float HealingAmount )
	{
		if ( HealingAmount < 0 )
		{
			UnityEngine.Debug.LogException( new ArgumentOutOfRangeException( "Tried to apply Negative Healing!" ) );
		}

		Health += HealingAmount;

		if ( Health > SOLIDER_MAX_HEALTH )
			Health = SOLIDER_MAX_HEALTH;
	}

	/// <summary> Shoots the given target.</summary>
	/// <param name="target"> Target for the Solider.</param>
	public void Shoot( Solider target )
	{
		if ( !isMoving && canShoot )
		{

			if ( ( target.transform.position - transform.position ).magnitude < Range )
			{

				float angle = 0;

				Vector2 diff = target.transform.position - transform.position;

				angle = Mathf.Atan2( diff.y, diff.x );

				angle *= Mathf.Rad2Deg;

				//Debug.Log( "Angle: " + angle );

				transform.rotation = Quaternion.Euler( 0, 0, angle );

				//Random 50% change to hit

				float chance = Random.Range( 0.0f, 100.0f );

				if ( chance > 50 )
				{
					target.ApplyDamage( Damage + EffectedDamage );
				}

				canShoot = false;

				StartCoroutine( shotWaitTime() );
			}
		}
	}

	/// <summary> (Coroutine) Applies the effect.</summary>
	/// <param name="type">			  The type of the Effect.</param>
	/// <param name="effectAmount">   The amount the effect has on the type.</param>
	/// <param name="effectDuration"> Duration of the effect.</param>
	/// <returns> An IEnumerator.</returns>
	public IEnumerator ApplyEffect( EffectType type, float effectAmount, float effectDuration )
	{

		switch ( type )
		{
			case ( EffectType.Damage ):

				EffectedDamage += effectAmount;
				break;

			case ( EffectType.Health ):

				if ( effectAmount >= 0 )
				{
					ApplyHealing( effectAmount );
				}
				else
					ApplyDamage( effectAmount );

				yield break;

			case ( EffectType.Speed ):

				EffectedMovementSpeed += effectAmount;
				break;
		}

		yield return new WaitForSeconds( effectDuration );

		switch ( type )
		{
			case ( EffectType.Damage ):

				EffectedDamage -= effectAmount;

				break;

			case ( EffectType.Speed ):

				EffectedMovementSpeed -= effectAmount;

				break;
		}
	}

	private IEnumerator shotWaitTime()
	{
		yield return new WaitForSeconds( 1.0f / ShotPerSecond );
		canShoot = true;
	}

	private void calculateNearestEnemies()
	{
		Dictionary<float, Solider> EnemyList = new Dictionary<float, Solider>();

		List<Solider> L = Manager.Teams[ Team.Blue ];

		if ( AssignedTeam == Team.Blue )
			L = Manager.Teams[ Team.Red ];

		else if ( AssignedTeam == Team.Red )
			L = Manager.Teams[ Team.Blue ];

		for ( int i = 0; i < L.Count; i++ )
		{

			float dis = Vector3.Distance( L[ i ].transform.position, transform.position );
			EnemyList.Add( dis, L[ i ] );
		}

		List<float> SortedEnemyDis = new List<float>( 6 );

		SortedEnemyDis.AddRange( EnemyList.Keys );

		SortedEnemyDis.Sort();

		for ( int i = 0; i < SOLIDER_TRACK_ENEMY_COUNT && i < SortedEnemyDis.Count; i++ )
		{
			nearbyEnemies[ i ] = EnemyList[ SortedEnemyDis[ i ] ];
		}

	}

	private void calculateNearestFriendlies()
	{
		List<Solider> L = Manager.Teams[ AssignedTeam ];

		Dictionary<float, Solider> FriendlyList = new Dictionary<float, Solider>();

		for ( int i = 0; i < L.Count; i++ )
		{

			float dis = Vector3.Distance( L[ i ].transform.position, transform.position );
			FriendlyList.Add( dis, L[ i ] );
		}

		List<float> SortedFriendlyDis = new List<float>( 6 );

		SortedFriendlyDis.AddRange( FriendlyList.Keys );

		SortedFriendlyDis.Sort();

		for ( int i = 0; i < SOLIDER_TRACK_FRIENDLY_COUNT && i < SortedFriendlyDis.Count; i++ )
		{
			nearbyFriendlies[ i ] = FriendlyList[ SortedFriendlyDis[ i ] ];
		}
	}

	/// <summary>Train the neural network with a set of hardcoded training data.</summary>
	private void TrainNN()
	{
		// Set up a sufficent set of training data for the perceptron
		// Train the system to output a 1 for the state the system is in and 0 for every other state
		float[] NONE   = new float[] { 1, 0, 0, 0, 0, 0 };
		float[] FLEE   = new float[] { 0, 1, 0, 0, 0, 0 };
		float[] FIGHT  = new float[] { 0, 0, 1, 0, 0, 0 };
		float[] HEAL   = new float[] { 0, 0, 0, 1, 0, 0 };
		float[] PATROL = new float[] { 0, 0, 0, 0, 1, 0 };
		float[] FIND   = new float[] { 0, 0, 0, 0, 0, 1 };
		TrainingSet[] trainingData = new TrainingSet[] {
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, NONE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FLEE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIGHT  ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, HEAL   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, PATROL ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIND   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, NONE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FLEE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIGHT  ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, HEAL   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, PATROL ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIND   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, NONE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FLEE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIGHT  ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, HEAL   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, PATROL ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIND   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, NONE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FLEE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIGHT  ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, HEAL   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, PATROL ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIND   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, NONE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FLEE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIGHT  ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, HEAL   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, PATROL ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIND   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, NONE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FLEE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIGHT  ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, HEAL   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, PATROL ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIND   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, NONE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FLEE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIGHT  ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, HEAL   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, PATROL ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIND   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, NONE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FLEE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIGHT  ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, HEAL   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, PATROL ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIND   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, NONE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FLEE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIGHT  ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, HEAL   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, PATROL ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIND   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, NONE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FLEE   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIGHT  ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, HEAL   ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, PATROL ),
			new TrainingSet( new float[] {	  0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0	}, FIND   )
		};

		// Train the system with the above data
		// Note: Training will fail right now so it is disabled.
		//neuralNetwork.Train( trainingData, 1f, 1f, -0.2f, 0.2f ); 
	}

	/// <summary>Cycle the neural network and use its output to select the current AI state.</summary>
	private void SelectState()
	{
		// Compute distances to each friendly
		float[] friendlyDistances = new float[ 4 ];
		for ( int i = 0; i < friendlyDistances.Length; i++ )
		{
			if ( nearbyFriendlies[ i ] == null )
				friendlyDistances[ i ] = 10000f;
			else
				friendlyDistances[ i ] = Vector2.Distance( nearbyFriendlies[ i ].transform.position, transform.position );
		}

		// Compute distances to each enemy
		float[] enemyDistances = new float[ 5 ];
		for ( int i = 0; i < enemyDistances.Length; i++ )
		{
			if ( nearbyEnemies[ i ] == null )
				enemyDistances[ i ] = 10000f;
			else
				enemyDistances[ i ] = Vector2.Distance( nearbyEnemies[ i ].transform.position, transform.position );
		}

		// Read inputs into MLP inputs
		neuralNetwork.Inputs[  0 ].Value = Health;
		neuralNetwork.Inputs[  1 ].Value = ( nearbyFriendlies[ 0 ] != null ) ? nearbyFriendlies[ 0 ].Health : 0f;
		neuralNetwork.Inputs[  2 ].Value = ( nearbyFriendlies[ 1 ] != null ) ? nearbyFriendlies[ 1 ].Health : 0f;
		neuralNetwork.Inputs[  3 ].Value = ( nearbyFriendlies[ 2 ] != null ) ? nearbyFriendlies[ 2 ].Health : 0f;
		neuralNetwork.Inputs[  4 ].Value = ( nearbyFriendlies[ 3 ] != null ) ? nearbyFriendlies[ 3 ].Health : 0f;
		neuralNetwork.Inputs[  5 ].Value = Ammo;
		neuralNetwork.Inputs[  6 ].Value = ( nearbyFriendlies[ 0 ] != null ) ? nearbyFriendlies[ 0 ].Ammo : 0f;
		neuralNetwork.Inputs[  7 ].Value = ( nearbyFriendlies[ 1 ] != null ) ? nearbyFriendlies[ 1 ].Ammo : 0f;
		neuralNetwork.Inputs[  8 ].Value = ( nearbyFriendlies[ 2 ] != null ) ? nearbyFriendlies[ 2 ].Ammo : 0f;
		neuralNetwork.Inputs[  9 ].Value = ( nearbyFriendlies[ 3 ] != null ) ? nearbyFriendlies[ 3 ].Ammo : 0f;
		neuralNetwork.Inputs[ 10 ].Value = 0f;
		neuralNetwork.Inputs[ 11 ].Value = friendlyDistances[ 0 ];
		neuralNetwork.Inputs[ 12 ].Value = friendlyDistances[ 1 ];
		neuralNetwork.Inputs[ 13 ].Value = friendlyDistances[ 2 ];
		neuralNetwork.Inputs[ 14 ].Value = friendlyDistances[ 3 ];
		neuralNetwork.Inputs[ 15 ].Value = enemyDistances[ 0 ];
		neuralNetwork.Inputs[ 16 ].Value = enemyDistances[ 1 ];
		neuralNetwork.Inputs[ 17 ].Value = enemyDistances[ 2 ];
		neuralNetwork.Inputs[ 18 ].Value = enemyDistances[ 3 ];
		neuralNetwork.Inputs[ 19 ].Value = enemyDistances[ 4 ];

		// Cycle the MLP to compute its output
		neuralNetwork.Cycle();

		// Read outputs into AI state
		if ( neuralNetwork.Outputs[ (int)AIState.Flee ].Value > 0.8f )
		{
			aiState = AIState.Flee;
		}
		else if ( neuralNetwork.Outputs[ (int)AIState.Fight ].Value > 0.8f )
		{
			aiState = AIState.Fight;
		}
		else if ( neuralNetwork.Outputs[ (int)AIState.HealFriend ].Value > 0.8f )
		{
			aiState = AIState.HealFriend;
		}
		else if ( neuralNetwork.Outputs[ (int)AIState.Patrol ].Value > 0.8f )
		{
			aiState = AIState.Patrol;
		}
		else if ( neuralNetwork.Outputs[ (int)AIState.Find ].Value > 0.8f )
		{
			aiState = AIState.Find;
		}
		else
		{
			aiState = AIState.None;
		}
	}

	/// <summary> Executes the run away operation.</summary>
	private void RunAway()
	{
		Vector2 AverageDir = Vector2.zero;
		int count = 0;

		for ( int i = 0; i < SOLIDER_TRACK_ENEMY_COUNT; i++ )
		{
			if ( nearbyEnemies[ i ] != null )
			{
				count++;
				Vector2 dir = nearbyEnemies[ i ].transform.position - transform.position;
				dir.Normalize();
				AverageDir += dir;
			}
		}

		AverageDir /= count;

		AverageDir.Normalize();

		AverageDir *= -4;

		Vector2 tTarget;
		tTarget.x = AverageDir.x + transform.position.x;
		tTarget.y = AverageDir.y + transform.position.y;

		MoveTo( tTarget );
	}

	/// <summary> Executes the fight operation.</summary>
	private void Fight()
	{

		attackTarget = nearbyEnemies[ 0 ];

		float distance;
		try
		{
			distance = Vector2.Distance( transform.position, attackTarget.transform.position );
		}
		catch ( NullReferenceException e )
		{
			e.ToString();
			return;
		}

		if ( distance < Range )
		{
			isMoving = false;
			Shoot( attackTarget );
		}
		else
		{
			Vector2 dir = attackTarget.transform.position - transform.position;
			dir.Normalize();

			dir *= ( distance - Range );



			MoveTo( (Vector2)transform.position + dir );
		}
	}

	/// <summary> Executes the patrol operation</summary>
	private void Patrol()
	{

	}

	/// <summary> Executes the heal friendly operation.</summary>
	private void HealFriendly()
	{

	}

}
