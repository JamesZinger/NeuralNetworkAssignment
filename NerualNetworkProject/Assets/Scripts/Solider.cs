using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Diagnostics;


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
	
	/// <summary> The solider maximum health.</summary>
	public const float SOLIDER_MAX_HEALTH = 100;

	/// <summary> The solider maximum ammo.</summary>
	public const int SOLIDER_MAX_AMMO = 1000;

	#endregion

	/// <summary> The movement speed.</summary>
	public float MovementSpeed = 5.0f;

	/// <summary> The damage each shot does.</summary>
	public float Damage = 10.0f;

	/// <summary> The range that the solider can fire</summary>
	public float Range = 10.0f;

	/// <summary> The amount of shots per second.</summary>
	public float ShotPerSecond = 10.0f;

	public Team AssignedTeam;

	public Sprite AliveSprite;
	public Sprite DeadSprite;

	[HideInInspector]
	public LevelManager Manager;

	#region Private Memebers

	private float health = SOLIDER_MAX_HEALTH;

	private int ammo = SOLIDER_MAX_AMMO;

	private float EffectedDamage;
	private float EffectedMovementSpeed;

	private bool isMoving;

	private Vector2 target;

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
		get { return (Damage*ShotPerSecond); }
	}

	/// <summary> (read only) Gets the time between shots.</summary>
	public float TimeBetweenShots
	{
		get { return (1/ShotPerSecond); }
	}

	#endregion

	#region Unity Events

	void Awake()
	{
		rigidbody2D.isKinematic = true;
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

		target = new Vector2( 0, 0 );
		isMoving = false;

		if ( AssignedTeam == Team.Red )
			gameObject.renderer.material.color = Color.red;

		else if ( AssignedTeam == Team.Blue )
			gameObject.renderer.material.color = Color.blue;
	}

	void Update()
	{
		if ( Input.GetMouseButtonDown( 0 ) && AssignedTeam == Team.Red )
		{
			Vector3 pos = Camera.main.ScreenToWorldPoint( Input.mousePosition );

			target = new Vector2( pos.x, pos.y );
			isMoving = true;


		}
	}

	void FixedUpdate()
	{

		// Move Toward Target
		if ( isMoving )
		{

			Vector2 Direction = target - new Vector2( transform.position.x, transform.position.y );

			float theta = Mathf.Atan2( Direction.y, Direction.x );

			Direction.Normalize();

			theta = Mathf.Rad2Deg * theta;

			transform.rotation = Quaternion.AngleAxis( theta, new Vector3( 0, 0, 1 ) );

			rigidbody2D.velocity = ( Direction * MovementSpeed );

			Vector2 Diff = target - new Vector2( transform.position.x, transform.position.y );

			Diff.x = Mathf.Abs( Diff.x );
			Diff.y = Mathf.Abs( Diff.y );

			if ( Diff.x < 0.2f && Diff.y < 0.2f )
			{
				Stopwatch sw = new Stopwatch();
				sw.Start();

				isMoving = false;
				rigidbody2D.isKinematic = true;

				Dictionary<float, Solider> EnemyList = new Dictionary<float,Solider>();
				Dictionary<float, Solider> FriendlyList = new Dictionary<float,Solider>();

				if ( AssignedTeam == Team.Red )
				{
					List<Solider> L = Manager.Teams[ Team.Blue ];

					for ( int i = 0; i < L.Count; i++ )
					{

						float dis = Vector3.Distance( L[ i ].transform.position, transform.position );
						EnemyList.Add( dis, L[ i ] );
					}

					L = Manager.Teams[ Team.Red ];

					for ( int i = 0; i < L.Count; i++ )
					{

						float dis = Vector3.Distance( L[ i ].transform.position, transform.position );
						FriendlyList.Add( dis, L[ i ] );
					}

				}

				ArrayList SortedEnemyDis = new ArrayList();

				SortedEnemyDis.AddRange( EnemyList.Keys );

				SortedEnemyDis.Sort();

				ArrayList SortedFriendlyDis = new ArrayList();

				SortedFriendlyDis.AddRange( FriendlyList.Keys );

				SortedFriendlyDis.Sort();

				sw.Stop();

				UnityEngine.Debug.Log( "Operation Took: " + sw.ElapsedTicks + " Ticks");
			}

			if ( Manager.Debugging )
			{
				UnityEngine.Debug.DrawLine( transform.position, target, Color.green );
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
			rigidbody2D.isKinematic = true;
		}

	}

	#endregion

	/// <summary> Executes the death action.</summary>
	void OnDeath()
	{

		GetComponent<SpriteRenderer>().sprite = DeadSprite;
		Manager.DeregisterSolider( this );

	}

	/// <summary> Move to the specificed position.</summary>
	/// <param name="Position"> The position to move toward.</param>
	public void MoveTo( Vector2 Position )
	{
		target = Position;
		isMoving = true;
		rigidbody2D.isKinematic = false;
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
		if ( !isMoving )
		{
			transform.LookAt( target.transform );

			if ( ( target.transform.position - transform.position ).magnitude < Range )
			{
				//Random 50% change to hit

				float chance = UnityEngine.Random.Range( 0.0f, 100.0f );

				if ( chance > 50 )
				{
					target.ApplyDamage( Damage + EffectedDamage );

				}
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

}
