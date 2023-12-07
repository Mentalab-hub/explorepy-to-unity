using System.Collections;
using System.Collections.Generic;
using System.Threading;
using AsyncIO;
using NetMQ;
using NetMQ.Sockets;
using TMPro;
using UnityEngine;

public class zmq_handler
{
    /**
    * Class for handling communication via ZMQ using a publisher-subscriber pattern.
    */
    protected bool running;
    public int _port = 5555;
    protected string last_message;
    protected Thread _runner_thread;

    public zmq_handler()
    {
        _runner_thread = new Thread(Run);
    }

    protected void Run()
    {
        ForceDotNet.Force();
        using (SubscriberSocket client = new SubscriberSocket())
        {
            client.Connect($"tcp://localhost:{_port}"); // change to IP to connect to
            client.SubscribeToAnyTopic();

            while (running)
            {
                if (!client.TryReceiveFrameString(out var message))
                    continue;
                last_message = message;
            }
        }
        NetMQConfig.Cleanup();
    }

    public void Start()
    {
        running = true;
        _runner_thread.Start();
    }

    public void Stop()
    {
        running = false;
        // block main thread, wait for _runnerThread to finish its job first, so we can be sure that
        // _runnerThread will end before main thread end
        _runner_thread.Join();
    }

    public string pop_last_message()
    {
        string tmp = last_message;
        last_message = null;
        return tmp;
    }
}

public class sinus_glow : MonoBehaviour
{
    //public float speed = 5.0f;
    public float range = 0.2f;
    public float glow_length = 1.0f;

    public bool play_mode = false;

    public GameObject statusText;
    public GameObject winCanvas;

    public GameObject left;
    public GameObject right;
    public GameObject up;
    public GameObject down;

    public GameObject indicator_lamp;

    private Dictionary<string, int> command_to_index;

    private Material left_mat;
    private Material right_mat;
    private Material up_mat;
    private Material down_mat;

    private Material indicator_mat;

    private Material[] mat_list;

    private float animation_timestamp = 0.0f;
    private bool isAnimating = false;
    private float last_message_requested_timestamp = 0.0f;
    private float wait_before_next_msg = 1.0f;
    private string last_message = null;
    private bool handle_next = true;
    private bool is_animating_win = false;

    private bool await_input = false;
    private int sequence_length = 3;
    private int[] current_sequence = null;
    private int sequence_pointer = 0;

    private KeyCode[] keycode_indices;

    private zmq_handler zmq_handler;

    // Start is called before the first frame update
    void Start()
    {
        zmq_handler = new zmq_handler();
        zmq_handler.Start();

        command_to_index = new Dictionary<string, int>();
        command_to_index.Add("left", 0);
        command_to_index.Add("right", 1);
        command_to_index.Add("up", 2);
        command_to_index.Add("down", 3);

        left_mat = left.GetComponent<Renderer>().material;
        right_mat = right.GetComponent<Renderer>().material;
        up_mat = up.GetComponent<Renderer>().material;
        down_mat = down.GetComponent<Renderer>().material;

        indicator_mat = indicator_lamp.GetComponent<Renderer>().material;

        left_mat.EnableKeyword("_EMISSION");
        right_mat.EnableKeyword("_EMISSION");
        up_mat.EnableKeyword("_EMISSION");
        down_mat.EnableKeyword("_EMISSION");

        indicator_mat.EnableKeyword("_EMISSION");

        mat_list = new Material[4];
        mat_list[0] = left_mat;
        mat_list[1] = right_mat;
        mat_list[2] = up_mat;
        mat_list[3] = down_mat;

        keycode_indices = new KeyCode[4];
        keycode_indices[0] = KeyCode.LeftArrow;
        keycode_indices[1] = KeyCode.RightArrow;
        keycode_indices[2] = KeyCode.UpArrow;
        keycode_indices[3] = KeyCode.DownArrow;
    }

    // Update is called once per frame
    void Update()
    {
        if (is_animating_win)
        {
            // Called when the player has successfully guessed a sequence
            StartCoroutine(animate_win());
        }
        else if (!play_mode)
        {
            // Executed if messages should be handled from ZMQ and not from keyboard presses
            if (!await_input)
            {
                // Play the initial sequence
                play_sequence();
            }
            else
            {
                // Look for input from ZMQ
                if (last_message_requested_timestamp >= wait_before_next_msg && !isAnimating)
                {
                    // Wait until wait_before_next_msg has passed before requesting a new message from ZMQ
                    last_message_requested_timestamp = 0.0f;
                    last_message = zmq_handler.pop_last_message();
                    Debug.Log("Reading message from zmq_handler: " + last_message);
                }
                last_message_requested_timestamp += Time.deltaTime;
                if (last_message != null)
                {
                    // Handle the message received from ZMQ
                    isAnimating = true;
                    if (handle_next == true)
                    {
                        // Handle the message only once (until light-up animation has finished and enough time has passed)
                        handle_incoming_msg(last_message);
                        handle_next = false;
                    }
                    // light up the respective box over time
                    animate_child(mat_list[command_to_index[last_message]]);
                    if (animation_timestamp >= glow_length)
                    {
                        // Reset values if animation has ended (played for glow_length time)
                        animation_timestamp = 0.0f;
                        isAnimating = false;
                        last_message = null;
                        handle_next = true;
                    }
                }
            }
        }
        else
        {
            // If in play mode, play always-repeating Simon Says
            if (!await_input)
            {
                play_sequence();
            }
            else
            {
                handle_input();
            }
        }
    }

    IEnumerator animate_win()
    {
        winCanvas.SetActive(true);
        yield return new WaitForSeconds(1.5f);
        winCanvas.SetActive(false);
        is_animating_win = false;
    }

    private void play_sequence()
    {
        if (current_sequence == null)
        {
            statusText.GetComponent<TextMeshProUGUI>().SetText("Playing sequence...");
            Debug.Log("Creating new sequence!");
            current_sequence = create_sequence(sequence_length, 4);
        }

        // Highlight current box from sequence
        animate_child(mat_list[current_sequence[sequence_pointer]]);

        // Move onto next box to highlight
        if (animation_timestamp >= glow_length)
        {
            animation_timestamp = 0.0f;
            sequence_pointer += 1;
            // If highlighting all boxes in the sequence has finished, reset pointer and wait for user input
            if (sequence_pointer >= current_sequence.Length)
            {
                sequence_pointer = 0;
                await_input = true;
                Debug.Log("Finished playing sequence, awaiting input...");
                statusText.GetComponent<TextMeshProUGUI>().SetText("Awaiting user input...");
            }
        }
    }

    private void handle_incoming_msg(string msg)
    {
        if (current_sequence[sequence_pointer] == command_to_index[msg])
        {
            indicator_mat.SetColor("_EmissionColor", Color.green*0.5f);
            Debug.Log("Got the right message " + msg + ", moving on!");
            sequence_pointer += 1;
            if (sequence_pointer >= current_sequence.Length)
            {
                sequence_pointer = 0;
                current_sequence = null;
                await_input = false;
                indicator_mat.SetColor("_EmissionColor", Color.white*0.5f);
                is_animating_win = true;
            }
        }
        else
        {
            indicator_mat.SetColor("_EmissionColor", Color.red*0.5f);
            Debug.Log("Wrong msg! Got " + msg + ", expected " + current_sequence[sequence_pointer]);
        }
    }

    private void handle_input()
    {
        if (Input.anyKeyDown)
        {
            KeyCode current_keycode = keycode_indices[current_sequence[sequence_pointer]];
            if (Input.GetKeyDown(current_keycode))
            {
                indicator_mat.SetColor("_EmissionColor", Color.green*0.5f);
                Debug.Log("Pressed the right key, moving on!");
                sequence_pointer += 1;
                if (sequence_pointer >= current_sequence.Length)
                {
                    sequence_pointer = 0;
                    current_sequence = null;
                    await_input = false;
                    indicator_mat.SetColor("_EmissionColor", Color.white*0.5f);
                    is_animating_win = true;
                }
            }
            else
            {
                indicator_mat.SetColor("_EmissionColor", Color.red*0.5f);
                Debug.Log("Wrong key!");
            }
        }
    }

    private void OnDestroy()
    {
        zmq_handler.Stop();
    }

    int[] create_sequence(int length, int upper_limit)
    {
        int[] new_sequence = new int[length];
        for (int i = 0; i < length; i++)
        {
            new_sequence[i] = Random.Range(0, upper_limit);
        }
        return new_sequence;
    }

    void animate_child(Material m)
    {
        float val = Mathf.Sin((1.0f / glow_length) * animation_timestamp * Mathf.PI);
        m.SetColor("_EmissionColor", Color.white * val * range);
        animation_timestamp += Time.deltaTime;
    }
}
