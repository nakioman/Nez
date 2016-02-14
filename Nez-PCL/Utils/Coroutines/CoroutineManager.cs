﻿using System;
using System.Collections.Generic;
using System.Collections;


namespace Nez.Systems
{
	/// <summary>
	/// basic CoroutineManager. Coroutines can do the following:
	/// - yield return null (tick again the next frame)
	/// - yield return 3 (tick again after a 3 second delay)
	/// - yield return 5.5 (tick again after a 5.5 second delay)
	/// - yield return startCoroutine( another() ) (wait for the other coroutine before getting ticked again)
	/// </summary>
	public class CoroutineManager
	{
		/// <summary>
		/// internal class used by the CoroutineManager to hide the data it requires for a Coroutine
		/// </summary>
		class CoroutineImpl : ICoroutine
		{
			public IEnumerator enumerator;
			/// <summary>
			/// anytime a delay is yielded it is added to the waitTimer which tracks the delays
			/// </summary>
			public float waitTimer;
			public bool isDone;
			public CoroutineImpl waitForCoroutine;


			public void stop()
			{
				isDone = true;
			}


			internal void reset()
			{
				waitTimer = 0;
				isDone = false;
				waitForCoroutine = null;
			}
		}


		/// <summary>
		/// flag to keep track of when we are in our update loop. If a new coroutine is started during the update loop we have to stick
		/// it in the shouldRunNextFrame List to avoid modifying a List while we iterate.
		/// </summary>
		bool _isInUpdate;
		List<CoroutineImpl> _unblockedCoroutines = new List<CoroutineImpl>();
		List<CoroutineImpl> _shouldRunNextFrame = new List<CoroutineImpl>();


		/// <summary>
		/// adds the IEnumerator to the CoroutineManager. Coroutines get ticked before Update is called each frame.
		/// </summary>
		/// <returns>The coroutine.</returns>
		/// <param name="enumerator">Enumerator.</param>
		public ICoroutine startCoroutine( IEnumerator enumerator )
		{
			// find or create a CoroutineImpl
			var coroutine = QuickCache<CoroutineImpl>.pop();

			// setup the coroutine and add it
			coroutine.enumerator = enumerator;
			coroutine.reset();
			tickCoroutine( coroutine );

			// guard against empty coroutines
			if( coroutine.isDone )
				return null;

			if( _isInUpdate )
				_shouldRunNextFrame.Add( coroutine );
			else
				_unblockedCoroutines.Add( coroutine );

			return coroutine;
		}


		public void update()
		{
			_isInUpdate = true;
			for( var i = 0; i < _unblockedCoroutines.Count; i++ )
			{
				var coroutine = _unblockedCoroutines[i];

				// check for stopped coroutines
				if( coroutine.isDone )
				{
					coroutine.isDone = true;
					coroutine.enumerator = null;
					QuickCache<CoroutineImpl>.push( coroutine );
					continue;
				}

				// are we waiting for any other coroutines to finish?
				if( coroutine.waitForCoroutine != null )
				{
					if( coroutine.waitForCoroutine.isDone )
					{
						coroutine.waitForCoroutine = null;
					}
					else
					{
						_shouldRunNextFrame.Add( coroutine );
						continue;
					}
				}

				// deal with timers if we have them
				if( coroutine.waitTimer > 0 )
				{
					// still has time left. decrement and run again next frame
					coroutine.waitTimer -= Time.deltaTime;
					_shouldRunNextFrame.Add( coroutine );
					continue;
				}

				tickCoroutine( coroutine );
			}

			_unblockedCoroutines.Clear();
			_unblockedCoroutines.AddRange( _shouldRunNextFrame );
			_shouldRunNextFrame.Clear();

			_isInUpdate = false;
		}


		void tickCoroutine( CoroutineImpl coroutine )
		{
			// This coroutine has finished
			if( !coroutine.enumerator.MoveNext() )
			{
				coroutine.isDone = true;
				coroutine.enumerator = null;
				QuickCache<CoroutineImpl>.push( coroutine );
				return;
			}

			if( coroutine.enumerator.Current is int )
			{
				var wait = (int)coroutine.enumerator.Current;
				coroutine.waitTimer = wait;
				_shouldRunNextFrame.Add( coroutine );
			}
			else if( coroutine.enumerator.Current is float )
			{
				var wait = (float)coroutine.enumerator.Current;
				coroutine.waitTimer = wait;
				_shouldRunNextFrame.Add( coroutine );
			}
			else if( coroutine.enumerator.Current is CoroutineImpl )
			{
				coroutine.waitForCoroutine = coroutine.enumerator.Current as CoroutineImpl;
				_shouldRunNextFrame.Add( coroutine );
			}
			else
			{
				// This coroutine yielded null, or some other value we don't understand. run it next frame.
				_shouldRunNextFrame.Add( coroutine );
			}
		}
	
	}
}

