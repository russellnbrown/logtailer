import os
from lorem.text import TextLorem
import logging
import time
import random

logging.basicConfig(filename='loggenerator.log', level=logging.DEBUG, format='%(asctime)s %(levelname)s %(message)s')
logging.info("Started")

lorem = TextLorem(wsep=' ', srange=(5,20) )

def generate():
    s1 = lorem.sentence() 
    level = random.randrange(1,5)
    if level == 1:
        logging.debug(s1)
    elif level == 2:
        logging.info(s1)
    elif level == 3:
        logging.warning(s1)
    else:
        logging.error(s1)

for iter in range(1000000):
    generate()

for iter in range(1000000):
    generate()
    time.sleep(0.05)



