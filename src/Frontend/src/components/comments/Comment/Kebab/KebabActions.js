import React, { useState, useEffect } from "react";
import PropTypes from "prop-types";
import { comment, userType, userRoles } from "../../commonPropTypes";
import Kebab from "@skbkontur/react-ui/components/Kebab/Kebab";
import MenuItem from "@skbkontur/react-ui/components/MenuItem/MenuItem";
import Icon from "@skbkontur/react-icons";
import { ACCESSES, SLIDETYPE } from "../../../../consts/general";

import styles from "./KebabActions.less";

function useIsMobileView() {
	const handleWindowResize = () => {
		setIsMobileView(document.documentElement.clientWidth < 768);
	};
	const [isMobileView, setIsMobileView] = useState(false);
	useEffect(() => {
		window.addEventListener('resize', handleWindowResize);

		return () => window.removeEventListener('resize', handleWindowResize);
	}, []);

	return isMobileView;
}

export default function KebabActions(props) {
	const {user, comment, userRoles, url, canModerateComments, actions, slideType} = props;
	const canModerate = canModerateComments(userRoles, ACCESSES.editPinAndRemoveComments);
	const canDeleteAndEdit = (user.id === comment.author.id || canModerate);
	const canSeeSubmissions = (slideType === SLIDETYPE.exercise &&
		canModerateComments(userRoles, ACCESSES.viewAllStudentsSubmissions));
	const isMobileView = useIsMobileView();

	return (
		<div className={styles.instructorsActions}>
			<Kebab positions={["left top"]} size="large" disableAnimations={true}>
				{canDeleteAndEdit &&
				<MenuItem
					icon={<Icon.Delete size="small" />}
					onClick={() => actions.handleDeleteComment(comment.id)}>
					Удалить
				</MenuItem>}
				{(canDeleteAndEdit && isMobileView) &&
				<MenuItem
					icon={<Icon.Edit size="small" />}
					onClick={() => actions.handleShowEditForm(comment.id)}>
					Редактировать
				</MenuItem>}
				{(canSeeSubmissions && isMobileView) &&
				<MenuItem
					href={url}
					icon={<Icon name="DocumentLite" size="small" />}>
					Посмотеть решения
				</MenuItem>}
				{canModerate &&
				<MenuItem
					icon={<Icon.EyeClosed size="small" />}
					onClick={() => actions.handleApprovedMark(comment.id, comment.isApproved)}>
					{!comment.isApproved ? "Опубликовать" : "Скрыть"}
				</MenuItem>}
				{(canModerate && comment.parentCommentId) &&
					<MenuItem
						onClick={() => actions.handleCorrectAnswerMark(comment.id, comment.isCorrectAnswer)}
						icon={<Icon name="Ok" size="small" />}>
						{comment.isCorrectAnswer ? "Снять отметку" : "Отметить правильным"}
					</MenuItem>}
				{(canModerate && !comment.parentCommentId) &&
					<MenuItem
						onClick={() => actions.handlePinnedToTopMark(comment.id, comment.isPinnedToTop)}
						icon={<Icon name="Pin" size="small" />}>
						{comment.isPinnedToTop ? "Открепить" : "Закрепить"}
					</MenuItem>}
			</Kebab>
		</div>
	)
}

KebabActions.propTypes = {
	comment: comment.isRequired,
	actions: PropTypes.objectOf(PropTypes.func),
	user: userType.isRequired,
	userRoles: userRoles.isRequired,
	url: PropTypes.string,
	canModerateComments: PropTypes.func,
	slideType: PropTypes.string,
};