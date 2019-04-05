import os
import random
import threading


'''ClusterServer.exe -p 8061 -n qqq -d 3000 -a'''
port = 8061
word = 'qqq'
delay = 1000
threads = []

delays = [10, 100]
for e in range(8060, 8080):
    threads.append(
        threading.Thread(
            target=os.system,
            args=(f'ClusterServer.exe -p {e} -n {word} -d {random.choice(delays)}',)))

for e in threads:
    e.start()