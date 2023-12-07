# ExplorePy to Unity communication example using ZMQ
This project implements a simple "Simon Says" game in which the player has to remember a sequence of 3 flashing cubes. The input to the game comes from a python server that publishes messages via ZMQ that are then handled by a subscribing thread in the Unity game.

## Python server interfacing with explorepy
The python server connects to an Explore device using the explorepy backend and sends a random string message (from "left", "right", "up", "down") via ZMQ when it receives an ExG packet from the device and the last message has been sent at least a second ago.

## Unity client
The game is entirely contained in the scene "ZMQSimonSays", all logic for the ZMQ interaction and the gameplay loop can be found in the "sinus_glow.cs" script. One instance of this script is attached to the GameObject "simon_says_pads".

The game lights up three of the visible cubes after another which the player has to replicate. In this example, the server generates random messages, so the game randomly plays itself.

A message is received (after a wait period) and checked against which cube lit up at this point in the sequence. The guessed sequence is not reset if the player guesses a wrong cube in the sequence, i.e. if the sequence is ["left", "right", "left"] and the player guesses ["left", "up", "right", "left"], the game will still register this guessed sequence as correct.

If the player guesses the correct sequence, a message is displayed that they've won and a new sequence is played.

A game mode exists in which the player can play the game with their arrow keys instead of ZMQ, to activate this, the "Play_mode" checkbox on the GameObject "simon_says_pads" has to be checked.

## Running the example
To run the example, both the python server has to be started as well as the game.

The server needs to connect to an Explore device to generate messages. Additionally, it requires ```explorepy``` and ```zmq``` to be installed. To do this, navigate to the folder ```python_zmq``` and run
```
pip install -r requirements.txt
```
To then start the server, run
```
python -m send_via_zmq.py Explore_XXXX
```
where ```Explore_XXXX``` should be replaced with your device's name. The default port used by the script for ZMQ communication is 5555, to set it you can provide another port using the ```--port``` argument (i.e. ```--port 1234```).

The Unity example listens on port 5555, if another port should be used, it needs to be edited in the ```sinus_glow.cs``` script.

To run the example, open the project folder ```ZMQSimonSays``` in the Unity Editor, navigate to the scene "SimonSaysScene" if it isn't open already (found in the Scenes folder) and click on the play button to run the example through the editor. The game will then start and flash three cubes in succession as previously described before getting input via ZMQ. If the server isn't running at this point, the game will simply keep listening and react when a new message arrives.