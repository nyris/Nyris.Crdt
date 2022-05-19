import http from 'k6/http';
import { sleep } from 'k6';

export default function () {
	http.post('http://localhost:5010/users');
	sleep(1);
}
