import moment from "moment";
import { DEFAULT_TIMEZONE } from "../consts/general";

export default function getMoment(time) {
	return moment(moment.tz(time, DEFAULT_TIMEZONE).format()).fromNow();
}

export function getDateDDMMYY(time) {
	return moment(time).format('DD MMMM YYYY в HH:mm');
}

export function convertDefaultTimezoneToLocal(timeInDefaultTimezone) {
	return moment.tz(timeInDefaultTimezone, DEFAULT_TIMEZONE).local();
}
