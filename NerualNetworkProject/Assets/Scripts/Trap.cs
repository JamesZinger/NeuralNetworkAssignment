using UnityEngine;
using System.Collections;

[RequireComponent( typeof( Rigidbody2D ) )]
public class Trap : MonoBehaviour
{

	public enum TrapType
	{
		Snare,
		Bear
	}

	TrapType type = TrapType.Snare;
	float EffectAmount = 10;
	float Duration = 10;

	// Use this for initialization
	void Start()
	{

	}

	// Update is called once per frame
	void Update()
	{

	}

	void OnCollisionEnter( Collision c )
	{
		Solider s = c.gameObject.GetComponent<Solider>();
		if ( s != null )
		{
			switch ( type )
			{
				case (TrapType.Bear):
					s.ApplyDamage( EffectAmount );
					break;
				case (TrapType.Snare):
					StartCoroutine( s.ApplyEffect( Solider.EffectType.Speed, EffectAmount, Duration ) );
					break;
			}
		}

	}
}
