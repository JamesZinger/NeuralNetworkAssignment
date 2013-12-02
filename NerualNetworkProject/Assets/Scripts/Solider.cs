using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

[RequireComponent( typeof( SpriteRenderer ) )]
[RequireComponent( typeof( Rigidbody2D ) )]

public class Solider : MonoBehaviour
{

	public enum Team : byte
	{
		Red,
		Blue
	}

	public enum EffectType : byte
	{
		Damage,
		Health,
		Speed
	}

	public float Health = 100;
	public int Ammo = 1000;
	public float MovementSpeed = 5;
	public float Damage = 10;
	public float Range = 10;

	public Team AssignedTeam;

	public Sprite AliveSprite;
	public Sprite DeadSprite;

	private float EffectedDamage;
	private float EffectedMovementSpeed;



	bool isMoving;

	[HideInInspector]
	public LevelManager Manager;

	private Vector2 target;

	void Awake()
	{
		rigidbody2D.isKinematic = true;
	}

	void Start()
	{
		Manager = GameObject.FindGameObjectWithTag( "Level" ).GetComponent<LevelManager>();

		if ( Health > Manager.SoliderMaxHealth )
		{
			Health = Manager.SoliderMaxHealth;
			Debug.LogWarning( "Solider health is greater than the levels max health" );
		}

		if ( Ammo > Manager.SoliderMaxAmmo )
		{
			Ammo = Manager.SoliderMaxAmmo;
			Debug.LogWarning( "Solider ammo is greater than the levels max ammo" );
		}

		if ( MovementSpeed > Manager.SoliderMaxMoveSpeed )
		{
			MovementSpeed = Manager.SoliderMaxMoveSpeed;
			Debug.LogWarning( "Solider movement speed is greater than the levels max movement speed" );
		}


		if ( Damage > Manager.SoliderMaxDamage )
		{
			Damage = Manager.SoliderMaxDamage;
			Debug.LogWarning( "Solider damage is greater than the levels max damage" );
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
				Debug.LogException( new ArgumentException( "Solider cannot have a null team" ) );

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
		if ( Input.GetMouseButtonDown( 0 ) && AssignedTeam == Team.Red)
		{
			Vector3 pos = Camera.main.ScreenToWorldPoint( Input.mousePosition );

			target = new Vector2( pos.x, pos.y );
			isMoving = true;

			SortedList<float, Solider> EnemyList = new SortedList<float,Solider>();
			SortedList<float, Solider> FriendlyList = new SortedList<float, Solider>();

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

			for ( int i = 0; i < EnemyList.Count; i++)
			{
				Debug.Log( "Distance At { " + i + " } : " + EnemyList.Keys[i] );
			}
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
				isMoving = false;
				rigidbody2D.isKinematic = true;
			}

			if ( Manager.Debugging )
			{
				Debug.DrawLine( transform.position, target, Color.green );
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

	void OnDeath()
	{

		GetComponent<SpriteRenderer>().sprite = DeadSprite;
		Manager.DeregisterSolider( this );

	}

	void MoveTo( Vector2 Position )
	{
		target = Position;
		isMoving = true;
		rigidbody2D.isKinematic = false;
	}

	public bool ApplyDamage( float AppliedDamage )
	{
		Health -= AppliedDamage;

		if ( Health <= 0 )
		{
			OnDeath();
			return true;
		}
		return false;
	}

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
					Health += effectAmount;
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
