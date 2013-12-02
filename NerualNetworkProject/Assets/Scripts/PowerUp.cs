using UnityEngine;
using System.Collections;

[RequireComponent( typeof( Rigidbody2D ) )]
public class PowerUp : MonoBehaviour
{

	public enum PowerUpType : byte
	{
		Damage,
		Health,
		Speed
	}

	public PowerUpType type;
	public float EffectAmount;
	public float EffectDuration;

	void Start()
	{

	}

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
				case ( PowerUpType.Damage ):

					StartCoroutine( s.ApplyEffect( Solider.EffectType.Damage, EffectAmount, EffectDuration ) );

					break;

				case ( PowerUpType.Health ):

					StartCoroutine( s.ApplyEffect( Solider.EffectType.Health, EffectAmount, EffectDuration ) );

					break;

				case ( PowerUpType.Speed ):

					StartCoroutine( s.ApplyEffect( Solider.EffectType.Speed, EffectAmount, EffectDuration ) );

					break;
			}
		}
	}

}
