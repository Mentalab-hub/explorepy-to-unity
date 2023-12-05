import argparse
import random
import time

import explorepy
from explorepy.stream_processor import TOPICS

import zmq


def create_socket(port):
    context = zmq.Context()
    s = context.socket(zmq.PUB)  # use publisher-subscriber pattern
    s.bind(f'tcp://*:{port}')
    return s


def create_args_parser():
    p = argparse.ArgumentParser()
    p.add_argument('device_name', type=str, help="Device to connect to (format: Explore_XXXX)")
    p.add_argument('--port', type=int, default=5555, help='Port to use for the ZMQ connection (defaults to 5555)')
    return p


def connect_explore_device(device_name):
    e = explorepy.Explore()
    e.connect(device_name)
    return e


class ExGProcessor:
    _gesture_dict = {0: 'left', 1: 'right', 2: 'up', 3: 'down'}

    def __init__(self, device_name, socket):
        self._last_ts = time.time()

        self.socket = socket

        self.explore = connect_explore_device(device_name)
        self.subscribe_all()

    def send_msg_via_zmq(self, msg):
        try:
            print(f"Sending string {msg}")
            self.socket.send_string(msg, flags=zmq.NOBLOCK, copy=True, track=False)
        except zmq.ZMQError as e:
            print("Could not send string!")
            print(e)

    def subscribe_all(self):
        self.explore.stream_processor.subscribe(callback=self.process_exg_packet, topic=TOPICS.raw_ExG)

    def process_exg_packet(self, packet):
        """Processes incoming ExG packets, creates a string message and passes it on to be published via ZMQ if the
        last msg was sent at least a second ago. In this example, the message sent is a random string from
        self._gesture_dict.
        :param packet: ExG packet arriving from explorepy
        """
        now = time.time()
        if (now - self._last_ts) >= 1:
            self._last_ts = now
            gesture = random.randint(0, 3)
            msg = self._gesture_dict[gesture]

            self.send_msg_via_zmq(msg)

    def disconnect_all(self):
        self.explore.disconnect()
        self.socket.close()


if __name__ == '__main__':
    parser = create_args_parser()
    args = parser.parse_args()

    socket = create_socket(args.port)
    exg_processor = ExGProcessor(args.device_name, socket)

    try:
        while True:
            time.sleep(0.5)
    except KeyboardInterrupt:
        print("Received KeyboardInterrupt, disconnecting and quitting...")
        exg_processor.disconnect_all()
