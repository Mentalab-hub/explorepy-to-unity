import zmq


def create_socket(port):
    context = zmq.Context()
    s = context.socket(zmq.SUB)
    s.setsockopt_string(zmq.SUBSCRIBE, "")
    s.connect(f'tcp://localhost:{port}')
    return s


if __name__ == '__main__':
    socket = create_socket(5555)
    try:
        while True:
            # Note: this blocks the thread, meaning if no messages are arriving, execution halts!
            msg = socket.recv_string()
            print(f"Received msg: {msg}")
    except KeyboardInterrupt:
        print("Received KeyboardInterrupt, quitting...")
        socket.close()
