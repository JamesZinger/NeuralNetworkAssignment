using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{

	public Vector2 MaxPosition;
	public Vector2 MinPosition;
	public Rect PlayableArea;

	public Dictionary<Solider.Team, List<Solider>> Teams;

	public List< Trap> Traps;

	public List<PowerUp> PowerUps;

	public bool Debugging = false;

	void Awake()
	{
		Teams = new Dictionary<Solider.Team, List<Solider>>();
		Teams[ Solider.Team.Red ] = new List<Solider>();
		Teams[ Solider.Team.Blue ] = new List<Solider>();

		Traps = new List<Trap>();

		PowerUps = new List<PowerUp>();
	}

	void Start()
	{
		// FInd the min and Max Position
		MaxPosition = Camera.main.ViewportToWorldPoint( new Vector3( 1, 1, 0 ) );
		MinPosition = Camera.main.ViewportToWorldPoint( new Vector3( 0, 0, 0 ) );


		PlayableArea = new Rect( MinPosition.x, MinPosition.y, MaxPosition.x - MinPosition.x, MaxPosition.y - MinPosition.y );

		transform.position = new Vector3( transform.position.x, transform.position.y, (transform.localScale.z / 2) + 1) ;

		if ( Debugging )
		{

			Ray r = Camera.main.ViewportPointToRay( new Vector3( 1, 1, 0 ) );
			Debug.DrawRay( r.origin, r.direction * 10000, Color.yellow, 10000 );

			r = Camera.main.ViewportPointToRay( new Vector3( 0, 0, 0 ) );
			Debug.DrawRay( r.origin, r.direction * 10000, Color.yellow, 10000 );

			Vector3 TopLeft = new Vector3( PlayableArea.xMin, PlayableArea.yMax, 0 );
			Vector3 TopRight = new Vector3( PlayableArea.xMax, PlayableArea.yMax, 0 );
			Vector3 BottomLeft = new Vector3( PlayableArea.xMin, PlayableArea.yMin, 0 );
			Vector3 BottomRight = new Vector3( PlayableArea.xMax, PlayableArea.yMin, 0 );

			Debug.DrawLine( TopLeft, TopRight, Color.red, 10000, false );
			Debug.DrawLine( TopLeft, BottomLeft, Color.red, 10000, false );
			Debug.DrawLine( BottomRight, TopRight, Color.red, 10000, false );
			Debug.DrawLine( BottomRight, BottomLeft, Color.red, 10000, false );
			Debug.DrawLine( TopLeft, BottomRight, Color.red, 10000, false );

			Debug.Log( "Top Left Playable Area: " + TopLeft.ToString() );
			Debug.Log( "Bottom Right Playable Area: " + BottomRight.ToString() );

		}
	}

	#region Registation Functions

	public void RegisterSolider( Solider s )
	{
		Teams[ s.AssignedTeam ].Add( s );
	}

	public void DeregisterSolider( Solider s )
	{
		Teams[ s.AssignedTeam ].Remove( s );
	}

	public void RegisterTrap( Trap t )
	{
		Traps.Add( t );
	}

	public void DeregisterTrap( Trap t )
	{
		Traps.Remove( t );
	}

	public void RegisterPowerup( PowerUp p )
	{
		PowerUps.Add( p );
	}

	public void DeregisterPowerup( PowerUp p )
	{
		PowerUps.Remove( p );
	}

	#endregion

	void Update()
	{

	}
}
